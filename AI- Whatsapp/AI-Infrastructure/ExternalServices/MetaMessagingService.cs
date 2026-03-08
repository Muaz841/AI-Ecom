using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Infrastructure.Persistence;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly ClientRepository _clientRepository;
    private readonly ITokenProtector _tokenProtector;
    private readonly IApplicationLogger _appLogger;
    private readonly ResiliencePipeline _sendPipeline;
    private readonly string _graphVersion;

    public const string MetaHttpClientName = "MetaGraphApi";
    public const string SendPipelineKey = "meta-send";    
    public const string HttpClientName = MetaHttpClientName;
    public const string SendMessagePipeline = SendPipelineKey;

    public MetaMessagingService(
        IHttpClientFactory httpClientFactory,
        PlatformDbContext dbContext,
        ClientRepository clientRepository,
        ITokenProtector tokenProtector,
        IApplicationLogger appLogger,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<MetaOAuthSettings> metaOAuthSettings)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clientRepository = clientRepository ?? throw new ArgumentNullException(nameof(clientRepository));
        _tokenProtector = tokenProtector ?? throw new ArgumentNullException(nameof(tokenProtector));
        _appLogger = appLogger ?? throw new ArgumentNullException(nameof(appLogger));
        var configuredVersion = metaOAuthSettings.Value.GraphVersion?.Trim();
        _graphVersion = string.IsNullOrWhiteSpace(configuredVersion) ? "25.0" : configuredVersion;

        _sendPipeline = pipelineRegistry.GetPipeline(SendPipelineKey)
            ?? throw new InvalidOperationException($"Resilience pipeline '{SendPipelineKey}' not found in registry.");
    }

    public async Task<MessagingSendResult> SendTextMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string messageText,
        string? messagingType = "RESPONSE",
        CancellationToken cancellationToken = default)
    {
        var accessToken = await ResolveAccessTokenAsync(clientId, platform, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _appLogger.Error("Cannot send message: Client {ClientId} not found or missing token", clientId);
            await TryWriteOutboundLogAsync(
                tenantId: clientId,
                operation: "send-text",
                endpoint: null,
                requestPayload: messageText,
                isSuccess: false,
                statusCode: 400,
                responsePayload: null,
                errorMessage: "Client configuration missing.");
            return new MessagingSendResult(false, null, "Client configuration missing", 400);
        }

        var httpClient = _httpClientFactory.CreateClient(MetaHttpClientName);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        string endpoint = platform.ToLowerInvariant() switch
        {
            "whatsapp" => $"v{_graphVersion}/{recipient}/messages",
            "instagram" => $"v{_graphVersion}/me/messages",
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
                _appLogger.Info("Message sent to {Recipient} on {Platform} for client {ClientId}", recipient, platform, clientId);
                await TryWriteOutboundLogAsync(
                    tenantId: clientId,
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
                tenantId: clientId,
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
            _appLogger.Error(ex, "Exception sending message to Meta for client {ClientId}", clientId);
            await TryWriteOutboundLogAsync(
                tenantId: clientId,
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

    private async Task<string?> ResolveAccessTokenAsync(Guid clientId, string platform, CancellationToken cancellationToken)
    {
        var normalizedChannel = platform.Trim().ToLowerInvariant() switch
        {
            "whatsapp" => MetaChannelTypes.WhatsApp,
            "instagram" => MetaChannelTypes.Instagram,
            "facebook" => MetaChannelTypes.Facebook,
            _ => platform.Trim().ToLowerInvariant()
        };

        var connection = await _dbContext.MetaChannelConnections
            .AsNoTracking()
            .Where(x => x.TenantId == clientId && x.Channel == normalizedChannel && x.Status == MetaConnectionStatuses.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is not null)
        {
            try
            {
                return _tokenProtector.Unprotect(connection.AccessTokenCiphertext);
            }
            catch (Exception ex)
            {
                _appLogger.Error(ex, "Failed to decrypt tenant token for client {ClientId} channel {Channel}", clientId, normalizedChannel);
            }
        }

        // Backward compatibility fallback: legacy token on Client entity.
        var clientEntity = await _clientRepository.GetByIdAsync(clientId);
        return clientEntity?.MetaAccessToken;
    }

    public Task<MessagingSendResult> SendTemplateMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string templateName,
        object? templateParameters = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MessagingSendResult(false, null, "Template sending not implemented yet"));
    }

    public Task<MessagingSendResult> SendImageMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MessagingSendResult(false, null, "Image sending not implemented yet"));
    }

    public Task MarkMessageAsReadAsync(
        Guid clientId,
        string platform,
        string recipient,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        _appLogger.Info("Mark as read requested for message {MessageId} (stub)", messageId);
        return Task.CompletedTask;
    }

    public Task<MessagingSendResult> SendQuickRepliesAsync(
        Guid clientId,
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
