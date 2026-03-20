using System;

namespace EcomAI.Platform.Business.Entities;

public class ConversationThread : Entity<Guid>, ITenantEntity
{
    public string Platform { get; private set; } = null!;
    public string CustomerIdentifier { get; private set; } = null!;
    public string BusinessIdentifier { get; private set; } = null!;
    public string? CustomerDisplayName { get; private set; }
    public string? LastMessagePreview { get; private set; }
    public MessageDirection? LastMessageDirection { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }
    public bool IsOpen { get; private set; }
    public AssignmentMode AssignmentMode { get; private set; } = AssignmentMode.AI;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ConversationThread()
    {
    }

    public static ConversationThread Create(
        Guid tenantId,
        string platform,
        string customerIdentifier,
        string businessIdentifier,
        string? customerDisplayName = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(platform))
            throw new ArgumentException("Platform is required.", nameof(platform));

        if (string.IsNullOrWhiteSpace(customerIdentifier))
            throw new ArgumentException("Customer identifier is required.", nameof(customerIdentifier));

        if (string.IsNullOrWhiteSpace(businessIdentifier))
            throw new ArgumentException("Business identifier is required.", nameof(businessIdentifier));

        return new ConversationThread
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            Platform            = platform.Trim().ToLowerInvariant(),
            CustomerIdentifier  = customerIdentifier.Trim(),
            BusinessIdentifier  = businessIdentifier.Trim(),
            CustomerDisplayName = customerDisplayName?.Trim(),
            MessageCount        = 0,
            IsOpen              = true,
            AssignmentMode      = AssignmentMode.AI,
            CreatedAt           = DateTime.UtcNow
        };
    }

    public void TouchWithMessage(MessageDirection direction, string messageContent, DateTime occurredAtUtc)
    {
        LastMessageDirection = direction;
        LastMessagePreview   = string.IsNullOrWhiteSpace(messageContent)
            ? null
            : (messageContent.Length > 500 ? messageContent[..500] : messageContent);
        LastMessageAt        = occurredAtUtc;
        MessageCount        += 1;
        UpdatedAt            = DateTime.UtcNow;
    }

    public Message AddIncomingMessage(
        string from,
        string to,
        string content,
        string? rawPayloadJson = null,
        string? externalMessageId = null,
        MessageType messageType = MessageType.Text)
    {
        var message = Message.CreateIncoming(
            tenantId:             TenantId!.Value,
            platform:             Platform,
            from:                 from,
            to:                   to,
            content:              content,
            rawPayloadJson:       rawPayloadJson,
            conversationThreadId: Id,
            externalMessageId:    externalMessageId,
            messageType:          messageType);

        TouchWithMessage(MessageDirection.Incoming, content, message.ReceivedAt);
        return message;
    }

    public Message AddOutgoingMessage(
        string from,
        string to,
        string content,
        string? externalMessageId = null,
        MessageType messageType = MessageType.Text,
        string? rawPayloadJson = null)
    {
        var message = Message.CreateOutgoing(
            tenantId:             TenantId!.Value,
            platform:             Platform,
            from:                 from,
            to:                   to,
            content:              content,
            conversationThreadId: Id,
            externalMessageId:    externalMessageId,
            messageType:          messageType,
            rawPayloadJson:       rawPayloadJson);

        TouchWithMessage(MessageDirection.Outgoing, content, message.ReceivedAt);
        return message;
    }
}
