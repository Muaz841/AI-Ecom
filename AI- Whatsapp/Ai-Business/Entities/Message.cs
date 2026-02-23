using System;

namespace EcomAI.Platform.Business.Entities;

public class Message : Entity<Guid>
{
    public Guid ClientId { get; private set; }
    public string Platform { get; private set; } = null!;
    public string From { get; private set; } = null!;
    public string To { get; private set; } = null!;
    public string Content { get; private set; } = null!;
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
        Guid clientId,
        string platform,
        string from,
        string to,
        string content,
        string? rawPayloadJson = null)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("ClientId is required", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(platform) || !IsValidPlatform(platform))
        {
            throw new ArgumentException("Valid platform required (whatsapp or instagram)", nameof(platform));
        }

        if (string.IsNullOrWhiteSpace(from))
        {
            throw new ArgumentException("From is required", nameof(from));
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("To is required", nameof(to));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required", nameof(content));
        }

        return new Message
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Platform = platform.ToLowerInvariant().Trim(),
            From = from.Trim(),
            To = to.Trim(),
            Content = content.Trim(),
            ReceivedAt = DateTime.UtcNow,
            IsFromCustomer = true,
            IsHandledByAI = false,
            RawPayloadJson = rawPayloadJson
        };
    }

    public void MarkAsHandledByAI(string intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            throw new ArgumentException("Intent cannot be empty when marking as AI-handled");
        }

        IsHandledByAI = true;
        AiIntent = intent.Trim();
    }

    public void LinkToOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Valid OrderId required", nameof(orderId));
        }

        OrderId = orderId;
    }

    private static bool IsValidPlatform(string platform)
    {
        var lower = platform.ToLowerInvariant();
        return lower == "whatsapp" || lower == "instagram";
    }
}
