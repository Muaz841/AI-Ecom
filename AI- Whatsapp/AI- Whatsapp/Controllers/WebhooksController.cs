using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Api.Extensions;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using EcomAI.Platform.Infrastructure.Tenant;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ClientSecretsRepository _clientSecretsRepository;
    private readonly PlatformDbContext _dbContext;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IApplicationLogger _appLogger;
    private readonly IMetaOAuthRuntimeConfigProvider _configProvider;
    private readonly MetaWebhookSettings _webhookSettings;

    public WebhooksController(
        IMediator mediator,
        ClientSecretsRepository clientSecretsRepository,
        PlatformDbContext dbContext,
        ICurrentTenantAccessor tenantAccessor,
        IApplicationLogger appLogger,
        IMetaOAuthRuntimeConfigProvider configProvider,
        IOptions<MetaWebhookSettings> webhookSettings)
    {
        _mediator                 = mediator;
        _clientSecretsRepository  = clientSecretsRepository;
        _dbContext                = dbContext;
        _tenantAccessor           = tenantAccessor;
        _appLogger                = appLogger;
        _configProvider           = configProvider;
        _webhookSettings          = webhookSettings.Value;
    }
    

    [HttpGet("meta")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")]         string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")]    string? challenge)
    {
        var verifyToken = _webhookSettings.VerifyToken;
        if (string.IsNullOrWhiteSpace(verifyToken))
        {
            _appLogger.Warning("Webhook verify token is not configured.");
            return StatusCode(503, "Webhook not configured.");
        }

        if (mode == "subscribe" && token == verifyToken)
        {
            _appLogger.Info("Webhook verification successful");
            return Ok(challenge);
        }

        _appLogger.Warning("Webhook verification failed — token mismatch");
        return BadRequest("Verification failed");
    }
    

    [HttpPost("meta")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        
        var runtimeConfig = await _configProvider.GetRuntimeConfigAsync(cancellationToken);
        if (runtimeConfig is null)
        {
            _appLogger.Warning("Webhook received but platform Meta config is not set.");
            return StatusCode(503, "Platform not configured.");
        }

        if (!IsValidSignature(Request.Headers["X-Hub-Signature-256"], rawBody, runtimeConfig.AppSecret))
        {
            _appLogger.Warning("Webhook signature validation failed from IP {RemoteIp}",
                Request.HttpContext.Connection.RemoteIpAddress);
            await TryWriteWebhookLogAsync(null, rawBody, false, 401, "Invalid signature",
                "Webhook signature validation failed.", cancellationToken);
            return Unauthorized("Invalid signature");
        }

        var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(rawBody, CaseInsensitiveOptions);

        if (payload?.Entry == null || !payload.Entry.Any())
        {
            _appLogger.Warning("Invalid or empty webhook payload");
            await TryWriteWebhookLogAsync(null, rawBody, false, 400, "Invalid payload",
                "Webhook payload is empty or malformed.", cancellationToken);
            return BadRequest("Invalid payload");
        }

        
        return (payload.Object?.ToLowerInvariant()) switch
        {
            "whatsapp_business_account" =>
                await HandleWhatsAppWebhookAsync(payload, rawBody, cancellationToken),
            "instagram" =>
                await HandleInstagramOrFacebookWebhookAsync(payload, rawBody, MetaChannelTypes.Instagram, cancellationToken),
            "page" =>
                await HandleInstagramOrFacebookWebhookAsync(payload, rawBody, MetaChannelTypes.Facebook, cancellationToken),
            _ =>
                Ok() // Unknown object type; return 200 so Meta doesn't retry
        };
    }

    

    private async Task<IActionResult> HandleWhatsAppWebhookAsync(
        MetaWebhookPayload payload,
        string rawBody,
        CancellationToken ct)
    {
        var firstEntry = payload.Entry![0];
        var value = firstEntry.Changes?.FirstOrDefault()?.Value;

        // Resolve tenant via legacy ClientSecrets identifiers (WhatsApp WABA / phone ID)
        var clientSecrets = await _clientSecretsRepository.GetByMetaIdentifiersAsync(
            metaPageId: value?.PageId,
            whatsAppBusinessAccountId: value?.From?.BusinessAccountId ?? firstEntry.Id);

        if (clientSecrets is null)
        {
            // Also try resolving via MetaChannelAssets (WABA stored during OAuth)
            var wabaId = value?.From?.BusinessAccountId ?? firstEntry.Id;
            clientSecrets = await ResolveClientSecretsByAssetAsync(wabaId, MetaChannelTypes.WhatsApp, ct);
        }

        if (clientSecrets is null)
        {
            _appLogger.Warning("No matching tenant for WhatsApp webhook identifiers");
            await TryWriteWebhookLogAsync(null, rawBody, false, 404, "No matching tenant",
                "No tenant found for WhatsApp webhook.", ct);
            return NotFound("No matching tenant");
        }

        _tenantAccessor.SetCurrentTenantId(clientSecrets.TenantRefId);

        var processed = 0;
        foreach (var change in firstEntry.Changes ?? [])
        {
            var msgValue = change.Value;
            if (msgValue?.Messages == null) continue;

            foreach (var msg in msgValue.Messages)
            {
                var content = msg.Text?.Body;
                if (string.IsNullOrWhiteSpace(content)) continue;

                var command = new ProcessIncomingMessageCommand(
                    TenantId:        clientSecrets.TenantRefId,
                    Platform:        "whatsapp",
                    From:            msg.From ?? string.Empty,
                    To:              msgValue.Metadata?.PhoneNumberId ?? string.Empty,
                    Content:         content,
                    RawPayloadJson:  JsonSerializer.Serialize(msg),
                    ExternalMessageId: msg.Id);

                await _mediator.Send(command, ct);
                processed++;
            }
        }

        await TryWriteWebhookLogAsync(clientSecrets.TenantRefId, rawBody, true, 200,
            $"WhatsApp: {processed} message(s) processed", null, ct);
        return Ok();
    }

    // ── Instagram / Facebook DM handler ─────────────────────────────────────

    private async Task<IActionResult> HandleInstagramOrFacebookWebhookAsync(
        MetaWebhookPayload payload,
        string rawBody,
        string channel,
        CancellationToken ct)
    {
        var processed = 0;
        Guid? resolvedTenantId = null;

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var entryElements = root.TryGetProperty("entry", out var entriesJson)
            && entriesJson.ValueKind == JsonValueKind.Array
            ? entriesJson
            : default;

        for (var index = 0; index < payload.Entry!.Count; index++)
        {
            var entry = payload.Entry[index];
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

            // Direct messages (Instagram / Facebook Page DMs)
            foreach (var messaging in entry.Messaging ?? [])
            {
                var text = messaging.Message?.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                var senderId    = messaging.Sender?.Id ?? string.Empty;
                var recipientId = messaging.Recipient?.Id ?? entryId;
                var messageId   = messaging.Message?.Mid;

                var command = new ProcessIncomingMessageCommand(
                    TenantId:        tenantId.Value,
                    Platform:        channel,
                    From:            senderId,
                    To:              recipientId,
                    Content:         text,
                    RawPayloadJson:  JsonSerializer.Serialize(messaging),
                    ExternalMessageId: messageId,
                    MessageType: "text",
                    AllowAutoReply: true);

                await _mediator.Send(command, ct);
                processed++;
            }

            // Comments / mentions (Instagram + Facebook Page)
            if (entryElements.ValueKind == JsonValueKind.Array && index < entryElements.GetArrayLength())
            {
                processed += await HandleCommentChangesAsync(
                    tenantId.Value, channel, entryElements[index], entryId, ct);
            }
        }

        var label = channel == MetaChannelTypes.Instagram ? "Instagram" : "Facebook";
        await TryWriteWebhookLogAsync(resolvedTenantId, rawBody, true, 200,
            $"{label}: {processed} event(s) processed", null, ct);
        return Ok();
    }

    private async Task<int> HandleCommentChangesAsync(
        Guid tenantId,
        string channel,
        JsonElement entryElement,
        string entryId,
        CancellationToken ct)
    {
        if (!entryElement.TryGetProperty("changes", out var changes) ||
            changes.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var processed = 0;
        foreach (var change in changes.EnumerateArray())
        {
            if (!TryExtractCommentFromChange(change, entryId, out var comment))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(comment.Content) ||
                string.IsNullOrWhiteSpace(comment.FromId) ||
                string.IsNullOrWhiteSpace(comment.ToId))
            {
                continue;
            }

            var command = new ProcessIncomingMessageCommand(
                TenantId: tenantId,
                Platform: channel,
                From: comment.FromId,
                To: comment.ToId,
                Content: comment.Content,
                RawPayloadJson: comment.RawJson,
                ExternalMessageId: comment.ExternalId,
                MessageType: "comment",
                AllowAutoReply: false);

            await _mediator.Send(command, ct);
            processed++;
        }

        return processed;
    }

    // ── Tenant resolution helpers ────────────────────────────────────────────

    /// <summary>
    /// Looks up the tenant that owns the given Instagram Business Account ID.
    /// Must bypass the EF tenant filter since tenant context is unknown at this point.
    /// </summary>
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

    /// <summary>
    /// Fallback: resolve tenant by WABA or phone number stored as a MetaChannelAsset.
    /// Returns a synthetic ClientSecrets-like lookup result so the existing WhatsApp path continues to work.
    /// </summary>
    private async Task<ClientSecrets?> ResolveClientSecretsByAssetAsync(
        string? externalId,
        string channel,
        CancellationToken ct)
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

        // Return a synthetic ClientSecrets so the existing WhatsApp path can continue
        return await _clientSecretsRepository
            .FirstOrDefaultAsync(cs => cs.TenantRefId == asset.TenantId.Value);
    }

    // ── Signature validation ─────────────────────────────────────────────────

    private static bool IsValidSignature(string? signatureHeader, string payload, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedHex = signatureHeader["sha256=".Length..];
        byte[] expectedHash;
        try { expectedHash = Convert.FromHexString(expectedHex); }
        catch (FormatException) { return false; }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    // ── Logging ──────────────────────────────────────────────────────────────

    private async Task TryWriteWebhookLogAsync(
        Guid? tenantId,
        string requestPayload,
        bool isSuccess,
        int statusCode,
        string responsePayload,
        string? errorMessage,
        CancellationToken ct)
    {
        try
        {
            await _appLogger.LogIncomingAsync(
                tenantId:        tenantId,
                channel:         "meta-webhook",
                operation:       "receive",
                endpoint:        Request.Path.Value,
                requestPayload:  requestPayload,
                isSuccess:       isSuccess,
                statusCode:      statusCode,
                responsePayload: responsePayload,
                errorMessage:    errorMessage,
                correlationId:   HttpContext.TraceIdentifier,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _appLogger.Error(ex, "Failed to persist inbound webhook log.");
        }
    }

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record CommentEvent(
        string? ExternalId,
        string? FromId,
        string? ToId,
        string? Content,
        string RawJson);

    private static bool TryExtractCommentFromChange(
        JsonElement change,
        string? entryId,
        out CommentEvent comment)
    {
        comment = new CommentEvent(null, null, null, null, "{}");

        if (change.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var rawJson = change.GetRawText();
        if (!change.TryGetProperty("value", out var value))
        {
            return false;
        }

        var field = TryGetString(change, "field");

        // Accept common comment-related fields
        if (!string.IsNullOrWhiteSpace(field) &&
            !string.Equals(field, "comments", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field, "mentions", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field, "feed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var content = TryGetString(value, "text")
                   ?? TryGetString(value, "message");

        var externalId = TryGetString(value, "comment_id")
                      ?? TryGetString(value, "id");

        var fromId = TryGetNestedString(value, "from", "id")
                  ?? TryGetNestedString(value, "from", "username");

        var toId = TryGetString(value, "media_id")
                ?? TryGetString(value, "post_id")
                ?? entryId;

        comment = new CommentEvent(
            externalId,
            fromId,
            toId,
            content,
            rawJson);

        return true;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (!string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString()
                : prop.Value.ToString();
        }

        return null;
    }

    private static string? TryGetNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (!string.Equals(prop.Name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (prop.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return TryGetString(prop.Value, propertyName);
        }

        return null;
    }
}

// ── Options ──────────────────────────────────────────────────────────────────

/// <summary>Webhook-specific settings loaded from appsettings ("MetaWebhook" section).</summary>
public sealed class MetaWebhookSettings
{
    /// <summary>The hub.verify_token value Meta sends during webhook subscription verification.</summary>
    public string VerifyToken { get; set; } = string.Empty;
}

// ── WhatsApp webhook payload models ──────────────────────────────────────────

public sealed class MetaWebhookPayload
{
    [JsonPropertyName("object")] public string? Object { get; set; }
    public List<MetaEntry>? Entry { get; set; }
}

public sealed class MetaEntry
{
    public string? Id { get; set; }
    // WhatsApp shape
    public List<MetaChange>? Changes { get; set; }
    // Instagram / Facebook page shape
    public List<MetaInstagramMessaging>? Messaging { get; set; }
}

public sealed class MetaChange
{
    public MetaValue? Value { get; set; }
}

public sealed class MetaValue
{
    public string? MessagingProduct { get; set; }
    public MetaMetadata? Metadata { get; set; }
    public List<MetaMessage>? Messages { get; set; }
    public string? PageId { get; set; }
    public MetaFrom? From { get; set; }
}

public sealed class MetaMetadata
{
    public string? PhoneNumberId { get; set; }
}

public sealed class MetaMessage
{
    public string? From { get; set; }
    public string? Id { get; set; }
    public string? Type { get; set; }
    public MetaText? Text { get; set; }
}

public sealed class MetaText
{
    public string? Body { get; set; }
}

public sealed class MetaFrom
{
    public string? BusinessAccountId { get; set; }
}

// ── Instagram / Facebook DM payload models ────────────────────────────────────

public sealed class MetaInstagramMessaging
{
    public MetaInstagramUser? Sender { get; set; }
    public MetaInstagramUser? Recipient { get; set; }
    public long Timestamp { get; set; }
    public MetaInstagramMessage? Message { get; set; }
}

public sealed class MetaInstagramUser
{
    public string? Id { get; set; }
}

public sealed class MetaInstagramMessage
{
    public string? Mid { get; set; }
    public string? Text { get; set; }
}
