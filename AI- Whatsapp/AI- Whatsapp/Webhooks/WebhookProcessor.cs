using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using EcomAI.Platform.Infrastructure.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Api.Webhooks;

public sealed class WebhookProcessor : IWebhookProcessor
{
    private readonly IMediator                     _mediator;
    private readonly ClientSecretsRepository       _clientSecretsRepository;
    private readonly PlatformDbContext             _dbContext;
    private readonly ICurrentTenantAccessor        _tenantAccessor;
    private readonly IApplicationLogger            _appLogger;
    private readonly IMetaOAuthRuntimeConfigProvider _configProvider;
    private readonly MetaWebhookSettings           _webhookSettings;

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WebhookProcessor(
        IMediator                        mediator,
        ClientSecretsRepository          clientSecretsRepository,
        PlatformDbContext                dbContext,
        ICurrentTenantAccessor           tenantAccessor,
        IApplicationLogger               appLogger,
        IMetaOAuthRuntimeConfigProvider  configProvider,
        IOptions<MetaWebhookSettings>    webhookSettings)
    {
        _mediator                = mediator;
        _clientSecretsRepository = clientSecretsRepository;
        _dbContext               = dbContext;
        _tenantAccessor          = tenantAccessor;
        _appLogger               = appLogger;
        _configProvider          = configProvider;
        _webhookSettings         = webhookSettings.Value;
    }

    public async Task<WebhookProcessResult> ProcessAsync(
        string rawJson,
        bool skipSignature,
        string? signatureHeader,
        string endpoint,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // ── Signature validation ──────────────────────────────────────────────
        if (!skipSignature)
        {
            var runtimeConfig = await _configProvider.GetRuntimeConfigAsync(cancellationToken);
            if (runtimeConfig is null)
            {
                _appLogger.Warning("Webhook received but platform Meta config is not set.");
                return new WebhookProcessResult(false, 503, "Platform not configured.", 0, []);
            }

            if (!IsValidSignature(signatureHeader, rawJson, runtimeConfig.AppSecret))
            {
                _appLogger.Warning("Webhook signature validation failed.");
                await TryWriteWebhookLogAsync(null, rawJson, false, 401,
                    "Invalid signature", "Webhook signature validation failed.",
                    endpoint, correlationId, cancellationToken);
                return new WebhookProcessResult(false, 401, "Invalid signature.", 0, []);
            }
        }

        // ── Parse payload ────────────────────────────────────────────────────
        MetaWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MetaWebhookPayload>(rawJson, CaseInsensitiveOptions);
        }
        catch (JsonException)
        {
            await TryWriteWebhookLogAsync(null, rawJson, false, 400,
                "Invalid payload", "Webhook payload could not be deserialized.",
                endpoint, correlationId, cancellationToken);
            return new WebhookProcessResult(false, 400, "Invalid payload.", 0, []);
        }

        if (payload?.Entry == null || !payload.Entry.Any())
        {
            _appLogger.Warning("Invalid or empty webhook payload");
            await TryWriteWebhookLogAsync(null, rawJson, false, 400,
                "Empty payload", "Webhook payload is empty or malformed.",
                endpoint, correlationId, cancellationToken);
            return new WebhookProcessResult(false, 400, "Invalid or empty payload.", 0, []);
        }

        // ── Route to platform handler ────────────────────────────────────────
        return (payload.Object?.ToLowerInvariant()) switch
        {
            "whatsapp_business_account" =>
                await HandleWhatsAppAsync(payload, rawJson, endpoint, correlationId, cancellationToken),

            "instagram" =>
                await HandleInstagramOrFacebookAsync(payload, rawJson, MetaChannelTypes.Instagram,
                    endpoint, correlationId, cancellationToken),

            "page" =>
                await HandleInstagramOrFacebookAsync(payload, rawJson, MetaChannelTypes.Facebook,
                    endpoint, correlationId, cancellationToken),

            _ => new WebhookProcessResult(true, 200, "Unknown object type — ignored.", 0, [])
        };
    }

    // ── WhatsApp ─────────────────────────────────────────────────────────────

    private async Task<WebhookProcessResult> HandleWhatsAppAsync(
        MetaWebhookPayload payload,
        string rawJson,
        string endpoint,
        string correlationId,
        CancellationToken ct)
    {
        var firstEntry = payload.Entry![0];
        var value      = firstEntry.Changes?.FirstOrDefault()?.Value;

        var clientSecrets = await _clientSecretsRepository.GetByMetaIdentifiersAsync(
            metaPageId: value?.PageId,
            whatsAppBusinessAccountId: value?.From?.BusinessAccountId ?? firstEntry.Id);

        if (clientSecrets is null)
        {
            var wabaId = value?.From?.BusinessAccountId ?? firstEntry.Id;
            clientSecrets = await ResolveClientSecretsByAssetAsync(wabaId, MetaChannelTypes.WhatsApp, ct);
        }

        if (clientSecrets is null)
        {
            _appLogger.Warning("No matching tenant for WhatsApp webhook identifiers");
            await TryWriteWebhookLogAsync(null, rawJson, false, 404,
                "No matching tenant", "No tenant found for WhatsApp webhook.",
                endpoint, correlationId, ct);
            return new WebhookProcessResult(false, 404, "No matching tenant.", 0, []);
        }

        _tenantAccessor.SetCurrentTenantId(clientSecrets.TenantRefId);

        var messageResults = new List<ProcessIncomingMessageResult>();
        foreach (var change in firstEntry.Changes ?? [])
        {
            var msgValue = change.Value;
            if (msgValue?.Messages == null) continue;

            foreach (var msg in msgValue.Messages)
            {
                var content = msg.Text?.Body;
                if (string.IsNullOrWhiteSpace(content)) continue;

                var command = new ProcessIncomingMessageCommand(
                    TenantId:          clientSecrets.TenantRefId,
                    Platform:          "whatsapp",
                    From:              msg.From ?? string.Empty,
                    To:                msgValue.Metadata?.PhoneNumberId ?? string.Empty,
                    Content:           content,
                    RawPayloadJson:    JsonSerializer.Serialize(msg),
                    ExternalMessageId: msg.Id);

                var result = await _mediator.Send(command, ct);
                messageResults.Add(result);
            }
        }

        await TryWriteWebhookLogAsync(clientSecrets.TenantRefId, rawJson, true, 200,
            $"WhatsApp: {messageResults.Count} message(s) processed", null,
            endpoint, correlationId, ct);

        return new WebhookProcessResult(true, 200,
            $"WhatsApp: {messageResults.Count} message(s) processed.",
            messageResults.Count, messageResults);
    }

    // ── Instagram / Facebook ──────────────────────────────────────────────────

    private async Task<WebhookProcessResult> HandleInstagramOrFacebookAsync(
        MetaWebhookPayload payload,
        string rawJson,
        string channel,
        string endpoint,
        string correlationId,
        CancellationToken ct)
    {
        var messageResults = new List<ProcessIncomingMessageResult>();
        Guid? resolvedTenantId = null;

        using var doc   = JsonDocument.Parse(rawJson);
        var root        = doc.RootElement;
        var entryElements = root.TryGetProperty("entry", out var entriesJson)
                         && entriesJson.ValueKind == JsonValueKind.Array
            ? entriesJson
            : default;

        for (var index = 0; index < payload.Entry!.Count; index++)
        {
            var entry   = payload.Entry[index];
            var entryId = entry.Id;
            if (string.IsNullOrWhiteSpace(entryId)) continue;

            Guid? tenantId = channel == MetaChannelTypes.Instagram
                ? await ResolveTenantByIgAccountAsync(entryId, ct)
                : await ResolveTenantByPageIdAsync(entryId, ct);

            if (tenantId is null)
            {
                _appLogger.Warning("No tenant found for {Channel} entry {EntryId}", channel, entryId);
                continue;
            }

            resolvedTenantId ??= tenantId;
            _tenantAccessor.SetCurrentTenantId(tenantId.Value);

            // Direct messages
            foreach (var messaging in entry.Messaging ?? [])
            {
                var text = messaging.Message?.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                var command = new ProcessIncomingMessageCommand(
                    TenantId:          tenantId.Value,
                    Platform:          channel,
                    From:              messaging.Sender?.Id ?? string.Empty,
                    To:                messaging.Recipient?.Id ?? entryId,
                    Content:           text,
                    RawPayloadJson:    JsonSerializer.Serialize(messaging),
                    ExternalMessageId: messaging.Message?.Mid,
                    MessageType:       "text",
                    AllowAutoReply:    true);

                var result = await _mediator.Send(command, ct);
                messageResults.Add(result);
            }

            // Comments / mentions
            if (entryElements.ValueKind == JsonValueKind.Array && index < entryElements.GetArrayLength())
            {
                var commentResults = await HandleCommentChangesAsync(
                    tenantId.Value, channel, entryElements[index], entryId, ct);
                messageResults.AddRange(commentResults);
            }
        }

        var label = channel == MetaChannelTypes.Instagram ? "Instagram" : "Facebook";
        await TryWriteWebhookLogAsync(resolvedTenantId, rawJson, true, 200,
            $"{label}: {messageResults.Count} event(s) processed", null,
            endpoint, correlationId, ct);

        return new WebhookProcessResult(true, 200,
            $"{label}: {messageResults.Count} event(s) processed.",
            messageResults.Count, messageResults);
    }

    private async Task<List<ProcessIncomingMessageResult>> HandleCommentChangesAsync(
        Guid tenantId,
        string channel,
        JsonElement entryElement,
        string entryId,
        CancellationToken ct)
    {
        var results = new List<ProcessIncomingMessageResult>();

        if (!entryElement.TryGetProperty("changes", out var changes) ||
            changes.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var change in changes.EnumerateArray())
        {
            if (!TryExtractCommentFromChange(change, entryId, out var comment))
                continue;

            if (string.IsNullOrWhiteSpace(comment.Content)   ||
                string.IsNullOrWhiteSpace(comment.FromId)    ||
                string.IsNullOrWhiteSpace(comment.ToId))
                continue;

            var command = new ProcessIncomingMessageCommand(
                TenantId:          tenantId,
                Platform:          channel,
                From:              comment.FromId,
                To:                comment.ToId,
                Content:           comment.Content,
                RawPayloadJson:    comment.RawJson,
                ExternalMessageId: comment.ExternalId,
                MessageType:       "comment",
                AllowAutoReply:    false);

            var result = await _mediator.Send(command, ct);
            results.Add(result);
        }

        return results;
    }

    // ── Tenant resolution ─────────────────────────────────────────────────────

    private async Task<Guid?> ResolveTenantByIgAccountAsync(string igAccountId, CancellationToken ct)
    {
        var asset = await _dbContext.MetaChannelAssets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.ExternalId == igAccountId
                     && a.Channel    == MetaChannelTypes.Instagram
                     && a.IsActive)
            .FirstOrDefaultAsync(ct);

        return asset?.TenantId;
    }

    private async Task<Guid?> ResolveTenantByPageIdAsync(string pageId, CancellationToken ct)
    {
        var asset = await _dbContext.MetaChannelAssets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.ExternalId == pageId
                     && a.AssetType == MetaAssetTypes.Page
                     && a.IsActive)
            .FirstOrDefaultAsync(ct);

        return asset?.TenantId;
    }

    private async Task<ClientSecrets?> ResolveClientSecretsByAssetAsync(
        string? externalId, string channel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalId)) return null;

        var asset = await _dbContext.MetaChannelAssets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.ExternalId == externalId
                     && a.Channel    == channel
                     && a.IsActive)
            .FirstOrDefaultAsync(ct);

        if (asset?.TenantId is null) return null;

        return await _clientSecretsRepository
            .FirstOrDefaultAsync(cs => cs.TenantRefId == asset.TenantId.Value);
    }

    // ── Signature ─────────────────────────────────────────────────────────────

    private static bool IsValidSignature(string? signatureHeader, string payload, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedHex = signatureHeader["sha256=".Length..];
        byte[] expectedHash;
        try { expectedHash = Convert.FromHexString(expectedHex); }
        catch (FormatException) { return false; }

        using var hmac        = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computedHash      = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    private async Task TryWriteWebhookLogAsync(
        Guid?  tenantId,
        string requestPayload,
        bool   isSuccess,
        int    statusCode,
        string responsePayload,
        string? errorMessage,
        string endpoint,
        string correlationId,
        CancellationToken ct)
    {
        try
        {
            await _appLogger.LogIncomingAsync(
                tenantId:        tenantId,
                channel:         "meta-webhook",
                operation:       "receive",
                endpoint:        endpoint,
                requestPayload:  requestPayload,
                isSuccess:       isSuccess,
                statusCode:      statusCode,
                responsePayload: responsePayload,
                errorMessage:    errorMessage,
                correlationId:   correlationId,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _appLogger.Error(ex, "Failed to persist inbound webhook log.");
        }
    }

    // ── Comment extraction helpers ────────────────────────────────────────────

    private sealed record CommentEvent(
        string? ExternalId,
        string? FromId,
        string? ToId,
        string? Content,
        string  RawJson);

    private static bool TryExtractCommentFromChange(
        JsonElement change,
        string?     entryId,
        out CommentEvent comment)
    {
        comment = new CommentEvent(null, null, null, null, "{}");

        if (change.ValueKind != JsonValueKind.Object) return false;

        var rawJson = change.GetRawText();
        if (!change.TryGetProperty("value", out var value)) return false;

        var field = TryGetString(change, "field");
        if (!string.IsNullOrWhiteSpace(field)
            && !string.Equals(field, "comments",  StringComparison.OrdinalIgnoreCase)
            && !string.Equals(field, "mentions",  StringComparison.OrdinalIgnoreCase)
            && !string.Equals(field, "feed",      StringComparison.OrdinalIgnoreCase))
            return false;

        var content    = TryGetString(value, "text") ?? TryGetString(value, "message");
        var externalId = TryGetString(value, "comment_id") ?? TryGetString(value, "id");
        var fromId     = TryGetNestedString(value, "from", "id")
                      ?? TryGetNestedString(value, "from", "username");
        var toId       = TryGetString(value, "media_id")
                      ?? TryGetString(value, "post_id")
                      ?? entryId;

        comment = new CommentEvent(externalId, fromId, toId, content, rawJson);
        return true;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in element.EnumerateObject())
        {
            if (!string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase)) continue;
            return prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString()
                : prop.Value.ToString();
        }
        return null;
    }

    private static string? TryGetNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in element.EnumerateObject())
        {
            if (!string.Equals(prop.Name, objectName, StringComparison.OrdinalIgnoreCase)) continue;
            if (prop.Value.ValueKind != JsonValueKind.Object) return null;
            return TryGetString(prop.Value, propertyName);
        }
        return null;
    }
}
