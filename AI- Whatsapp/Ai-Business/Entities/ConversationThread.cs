using System;

namespace EcomAI.Platform.Business.Entities;

public class ConversationThread : Entity<Guid>, ITenantEntity
{
    public Guid ClientId { get; private set; }
    public string Platform { get; private set; } = null!;
    public string CustomerIdentifier { get; private set; } = null!;
    public string BusinessIdentifier { get; private set; } = null!;
    public string? CustomerDisplayName { get; private set; }
    public string? LastMessagePreview { get; private set; }
    public string? LastMessageDirection { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }
    public bool IsOpen { get; private set; }
    public string AssignmentMode { get; private set; } = "ai";
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ConversationThread()
    {
    }

    public static ConversationThread Create(
        Guid clientId,
        string platform,
        string customerIdentifier,
        string businessIdentifier,
        string? customerDisplayName = null)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("ClientId is required.", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("Platform is required.", nameof(platform));
        }

        if (string.IsNullOrWhiteSpace(customerIdentifier))
        {
            throw new ArgumentException("Customer identifier is required.", nameof(customerIdentifier));
        }

        if (string.IsNullOrWhiteSpace(businessIdentifier))
        {
            throw new ArgumentException("Business identifier is required.", nameof(businessIdentifier));
        }

        return new ConversationThread
        {
            Id = Guid.NewGuid(),
            TenantId = clientId,
            ClientId = clientId,
            Platform = platform.Trim().ToLowerInvariant(),
            CustomerIdentifier = customerIdentifier.Trim(),
            BusinessIdentifier = businessIdentifier.Trim(),
            CustomerDisplayName = customerDisplayName?.Trim(),
            MessageCount = 0,
            IsOpen = true,
            AssignmentMode = "ai",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void TouchWithMessage(string direction, string messageContent, DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            throw new ArgumentException("Direction is required.", nameof(direction));
        }

        LastMessageDirection = direction.Trim().ToLowerInvariant();
        LastMessagePreview = string.IsNullOrWhiteSpace(messageContent)
            ? null
            : (messageContent.Length > 500 ? messageContent[..500] : messageContent);
        LastMessageAt = occurredAtUtc;
        MessageCount += 1;
        UpdatedAt = DateTime.UtcNow;
    }
}
