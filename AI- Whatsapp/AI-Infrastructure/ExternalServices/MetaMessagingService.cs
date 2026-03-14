using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Registry;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class MetaMessagingService : IMetaMessagingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PlatformDbContext _dbContext;
    private readonly ITokenProtector _tokenProtector;
    private readonly IApplicationLogger _appLogger;
    private readonly ResiliencePipeline _sendPipeline;
    private readonly IMetaOAuthRuntimeConfigProvider _configProvider;

    public const string MetaHttpClientName = "MetaGraphApi";
    public const string SendPipelineKey    = "meta-send";
    public const string HttpClientName     = MetaHttpClientName;
    public const string SendMessagePipeline = SendPipelineKey;

    public MetaMessagingService(
        IHttpClientFactory httpClientFactory,
        PlatformDbContext dbContext,
        ITokenProtector tokenProtector,
        IApplicationLogger appLogger,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IMetaOAuthRuntimeConfigProvider configProvider)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _dbContext         = dbContext         ?? throw new ArgumentNullException(nameof(dbContext));
        _tokenProtector    = tokenProtector    ?? throw new ArgumentNullException(nameof(tokenProtector));
        _appLogger         = appLogger         ?? throw new ArgumentNullException(nameof(appLogger));
        _configProvider    = configProvider    ?? throw new ArgumentNullException(nameof(configProvider));

        _sendPipeline = pipelineRegistry.GetPipeline(SendPipelineKey)
            ?? throw new InvalidOperationException($"Resilience pipeline '{SendPipelineKey}' not found in registry.");
    }

    public async Task<MessagingSendResult> SendTextMessageAsync(
        Guid tenantId,
        string platform,
        string recipient,
        string messageText,
        string? messagingType = "RESPONSE",
        CancellationToken cancellationToken = default)
    {
        var accessToken = await ResolveAccessTokenAsync(tenantId, platform, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _appLogger.Error("Cannot send message: Tenant {TenantId} not found or missing token", tenantId);
            await TryWriteOutboundLogAsync(
                tenantId: tenantId,
                operation: "send-text",
                endpoint: null,
                requestPayload: messageText,
                isSuccess: false,
                statusCode: 400,
                responsePayload: null,
                errorMessage: "Tenant configuration missing.");
            return new MessagingSendResult(false, null, "Tenant configuration missing", 400);
        }

        var runtimeConfig = await _configProvider.GetRuntimeConfigAsync(cancellationToken);
        var graphVersion = string.IsNullOrWhiteSpace(runtimeConfig?.GraphVersion) ? "25.0" : runtimeConfig.GraphVersion;

        var httpClient = _httpClientFactory.CreateClient(MetaHttpClientName);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        string endpoint = platform.ToLowerInvariant() switch
        {
            "whatsapp" => $"v{graphVersion}/{recipient}/messages",
            "instagram" => $"v{graphVersion}/me/messages",
            "facebook" => $"v{graphVersion}/me/messages",
            _ => throw new ArgumentException($"Unsupported platform: {platform}", nameof(platform))
        };

        var payload = new
        {
            messaging_type = messagingType,
            recipient = new { id = recipient },
            message = new { text = messageText }
        };

        var requestContent = JsonContent.Create(payload);
        var requestPayload = await requestContent.ReadAsStringAsync(cancellationToken);

        try
        {
            var response = await _sendPipeline.ExecuteAsync(async ct =>
                await httpClient.PostAsync(endpoint, requestContent, ct), cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<MetaSendResponse>(cancellationToken: cancellationToken);
                _appLogger.Info("Message sent to {Recipient} on {Platform} for tenant {TenantId}", recipient, platform, tenantId);
                await TryWriteOutboundLogAsync(
                    tenantId: tenantId,
                    operation: "send-text",
                    endpoint: endpoint,
                    requestPayload: requestPayload,
                    isSuccess: true,
                    statusCode: (int)response.StatusCode,
                    responsePayload: await response.Content.ReadAsStringAsync(cancellationToken),
                    errorMessage: null);
                return new MessagingSendResult(true, result?.MessageId);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _appLogger.Error("Meta API error {StatusCode}: {Error}", response.StatusCode, errorContent);
                await TryWriteOutboundLogAsync(
                tenantId: tenantId,
                operation: "send-text",
                endpoint: endpoint,
                requestPayload: requestPayload,
                isSuccess: false,
                statusCode: (int)response.StatusCode,
                responsePayload: errorContent,
                errorMessage: errorContent);

            return new MessagingSendResult(false, null, errorContent, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _appLogger.Error(ex, "Exception sending message to Meta for tenant {TenantId}", tenantId);
            await TryWriteOutboundLogAsync(
                tenantId: tenantId,
                operation: "send-text",
                endpoint: endpoint,
                requestPayload: requestPayload,
                isSuccess: false,
                statusCode: null,
                responsePayload: null,
                errorMessage: ex.Message);
            return new MessagingSendResult(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Resolves the outbound access token for a tenant + channel.
    /// Priority:
    ///   1. Page-level asset token (required for Instagram/Facebook DM send)
    ///   2. Connection-level user token (fallback for WhatsApp or when no page asset yet)
    /// The legacy ClientSecrets fallback has been removed — connections must be established via OAuth.
    /// </summary>
    private async Task<string?> ResolveAccessTokenAsync(Guid tenantId, string platform, CancellationToken cancellationToken)
    {
        var normalizedChannel = platform.Trim().ToLowerInvariant() switch
        {
            "whatsapp"  => MetaChannelTypes.WhatsApp,
            "instagram" => MetaChannelTypes.Instagram,
            "facebook"  => MetaChannelTypes.Facebook,
            _           => platform.Trim().ToLowerInvariant()
        };

        // For Instagram and Facebook, prefer the page-level access token stored in MetaChannelAssets.
        // Outbound Instagram DM API requires a Page access token, not a user token.
        if (normalizedChannel is MetaChannelTypes.Instagram or MetaChannelTypes.Facebook)
        {
            var pageAsset = await _dbContext.MetaChannelAssets
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId
                         && a.Channel == normalizedChannel
                         && a.AssetType == MetaAssetTypes.Page
                         && a.IsActive
                         && a.PageAccessTokenCiphertext != null)
                .FirstOrDefaultAsync(cancellationToken);

            if (pageAsset?.PageAccessTokenCiphertext is not null)
            {
                try { return _tokenProtector.Unprotect(pageAsset.PageAccessTokenCiphertext); }
                catch (Exception ex)
                {
                    _appLogger.Error(ex, "Failed to decrypt page asset token for tenant {TenantId} channel {Channel}",
                        tenantId, normalizedChannel);
                }
            }
        }

        // Fall back to the connection-level long-lived user token (WhatsApp uses this directly)
        var connection = await _dbContext.MetaChannelConnections
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                     && x.Channel == normalizedChannel
                     && x.Status == MetaConnectionStatuses.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is not null)
        {
            try { return _tokenProtector.Unprotect(connection.AccessTokenCiphertext); }
            catch (Exception ex)
            {
                _appLogger.Error(ex, "Failed to decrypt connection token for tenant {TenantId} channel {Channel}",
                    tenantId, normalizedChannel);
            }
        }

        return null;
    }

    public Task<MessagingSendResult> SendTemplateMessageAsync(
        Guid tenantId,
        string platform,
        string recipient,
        string templateName,
        object? templateParameters = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MessagingSendResult(false, null, "Template sending not implemented yet"));
    }

    public Task<MessagingSendResult> SendImageMessageAsync(
        Guid tenantId,
        string platform,
        string recipient,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MessagingSendResult(false, null, "Image sending not implemented yet"));
    }

    public Task MarkMessageAsReadAsync(
        Guid tenantId,
        string platform,
        string recipient,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        _appLogger.Info("Mark as read requested for message {MessageId} (stub)", messageId);
        return Task.CompletedTask;
    }

    public Task<MessagingSendResult> SendQuickRepliesAsync(
        Guid tenantId,
        string platform,
        string recipient,
        string text,
        IEnumerable<QuickReplyOption> quickReplies,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MessagingSendResult(false, null, "Quick replies not implemented yet"));
    }

    private class MetaSendResponse
    {
        public string? MessageId { get; set; }
    }

    private async Task TryWriteOutboundLogAsync(
        Guid? tenantId,
        string operation,
        string? endpoint,
        string? requestPayload,
        bool isSuccess,
        int? statusCode,
        string? responsePayload,
        string? errorMessage)
    {
        try
        {
            await _appLogger.LogOutgoingAsync(
                tenantId: tenantId,
                channel: "meta-graph-api",
                operation: operation,
                endpoint: endpoint,
                requestPayload: requestPayload,
                isSuccess: isSuccess,
                statusCode: statusCode,
                responsePayload: responsePayload,
                errorMessage: errorMessage,
                correlationId: Guid.NewGuid().ToString("N"));
        }
        catch (Exception ex)
        {
            _appLogger.Error(ex, "Failed to persist outbound Meta API log.");
        }
    }
}
