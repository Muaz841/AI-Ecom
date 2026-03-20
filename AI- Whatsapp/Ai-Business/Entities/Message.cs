using System;

namespace EcomAI.Platform.Business.Entities;

public class Message : Entity<Guid>, ITenantEntity
{
    public Guid? ConversationThreadId { get; private set; }
    public string Platform { get; private set; } = null!;
    public string From { get; private set; } = null!;
    public string To { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public MessageDirection Direction { get; private set; }
    public MessageType MessageType { get; private set; }
    public string? ExternalMessageId { get; private set; }
    public DeliveryStatus? DeliveryStatus { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public bool IsFromCustomer { get; private set; }
    public bool IsHandledByAI { get; private set; }
    public string? AiIntent { get; private set; }
    public Guid? OrderId { get; private set; }
    public string? RawPayloadJson { get; private set; }

    private Message()
    {
    }

    public static Message CreateIncoming(
        Guid tenantId,
        string platform,
        string from,
        string to,
        string content,
        string? rawPayloadJson = null,
        Guid? conversationThreadId = null,
        string? externalMessageId = null,
        MessageType messageType = Business.MessageType.Text)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(platform) || !IsValidPlatform(platform))
            throw new ArgumentException("Valid platform required (whatsapp, instagram, or facebook)", nameof(platform));

        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException("From is required", nameof(from));

        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("To is required", nameof(to));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required", nameof(content));

        return new Message
        {
            Id                   = Guid.NewGuid(),
            TenantId             = tenantId,
            ConversationThreadId = conversationThreadId,
            Platform             = platform.ToLowerInvariant().Trim(),
            From                 = from.Trim(),
            To                   = to.Trim(),
            Content              = content.Trim(),
            Direction            = MessageDirection.Incoming,
            MessageType          = messageType,
            ExternalMessageId    = externalMessageId?.Trim(),
            ReceivedAt           = DateTime.UtcNow,
            IsFromCustomer       = true,
            IsHandledByAI        = false,
            RawPayloadJson       = rawPayloadJson
        };
    }

    public static Message CreateOutgoing(
        Guid tenantId,
        string platform,
        string from,
        string to,
        string content,
        Guid? conversationThreadId = null,
        string? externalMessageId = null,
        MessageType messageType = Business.MessageType.Text,
        string? rawPayloadJson = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(platform) || !IsValidPlatform(platform))
            throw new ArgumentException("Valid platform required (whatsapp, instagram, or facebook)", nameof(platform));

        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException("From is required", nameof(from));

        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("To is required", nameof(to));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required", nameof(content));

        return new Message
        {
            Id                   = Guid.NewGuid(),
            TenantId             = tenantId,
            ConversationThreadId = conversationThreadId,
            Platform             = platform.ToLowerInvariant().Trim(),
            From                 = from.Trim(),
            To                   = to.Trim(),
            Content              = content.Trim(),
            Direction            = MessageDirection.Outgoing,
            MessageType          = messageType,
            ExternalMessageId    = externalMessageId?.Trim(),
            ReceivedAt           = DateTime.UtcNow,
            IsFromCustomer       = false,
            IsHandledByAI        = true,
            RawPayloadJson       = rawPayloadJson
        };
    }

    public void MarkAsHandledByAI(string intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
            throw new ArgumentException("Intent cannot be empty when marking as AI-handled");

        IsHandledByAI = true;
        AiIntent = intent.Trim();
    }

    public void LinkToOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Valid OrderId required", nameof(orderId));

        OrderId = orderId;
    }

    public void MarkAsSent(DateTime sentAtUtc)
    {
        if (sentAtUtc == default)
            throw new ArgumentException("Sent timestamp is required", nameof(sentAtUtc));

        SentAt = sentAtUtc;
        DeliveryStatus = Business.DeliveryStatus.Sent;
    }

    public void SetDeliveryStatus(DeliveryStatus status)
    {
        DeliveryStatus = status;
    }

    private static bool IsValidPlatform(string platform)
    {
        var lower = platform.ToLowerInvariant();
        return lower == "whatsapp" || lower == "instagram" || lower == "facebook";
    }
}
