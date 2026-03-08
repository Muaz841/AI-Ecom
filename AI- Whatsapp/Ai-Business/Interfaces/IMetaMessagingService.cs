using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IMetaMessagingService
{
    Task<MessagingSendResult> SendTextMessageAsync(
        Guid TenantId,
        string platform,
        string recipient,
        string messageText,
        string? messagingType = "RESPONSE",
        CancellationToken cancellationToken = default);

    Task<MessagingSendResult> SendTemplateMessageAsync(
        Guid TenantId,
        string platform,
        string recipient,
        string templateName,
        object? templateParameters = null,
        CancellationToken cancellationToken = default);

    Task<MessagingSendResult> SendImageMessageAsync(
        Guid TenantId,
        string platform,
        string recipient,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default);

    Task MarkMessageAsReadAsync(
        Guid TenantId,
        string platform,
        string recipient,
        string messageId,
        CancellationToken cancellationToken = default);

    Task<MessagingSendResult> SendQuickRepliesAsync(
        Guid TenantId,
        string platform,
        string recipient,
        string text,
        IEnumerable<QuickReplyOption> quickReplies,
        CancellationToken cancellationToken = default);
}

public record MessagingSendResult(
    bool Success,
    string? MessageId,
    string? ErrorMessage = null,
    int StatusCode = 0);

public record QuickReplyOption(
    string Title,
    string Payload);

