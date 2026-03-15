using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EcomAI.Platform.Api.Webhooks;

// ── Settings ──────────────────────────────────────────────────────────────────

/// <summary>Webhook-specific settings loaded from appsettings ("MetaWebhook" section).</summary>
public sealed class MetaWebhookSettings
{
    /// <summary>The hub.verify_token value Meta sends during webhook subscription verification.</summary>
    public string VerifyToken { get; set; } = string.Empty;
}

// ── Top-level envelope ────────────────────────────────────────────────────────

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

// ── WhatsApp payload models ───────────────────────────────────────────────────

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
