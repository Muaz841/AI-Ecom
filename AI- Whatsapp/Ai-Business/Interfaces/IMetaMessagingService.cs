using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IMetaMessagingService
{
    Task<MessagingSendResult> SendTextMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string messageText,
        string? messagingType = "RESPONSE");

    Task<MessagingSendResult> SendTemplateMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string templateName,
        object? templateParameters = null);

    Task<MessagingSendResult> SendImageMessageAsync(
        Guid clientId,
        string platform,
        string recipient,
        string imageUrl,
        string? caption = null);

    Task MarkMessageAsReadAsync(
        Guid clientId,
        string platform,
        string recipient,
        string messageId);

    Task<MessagingSendResult> SendQuickRepliesAsync(
        Guid clientId,
        string platform,
        string recipient,
        string text,
        IEnumerable<QuickReplyOption> quickReplies);
}

public record MessagingSendResult(
    bool Success,
    string? MessageId,
    string? ErrorMessage = null,
    int StatusCode = 0);

public record QuickReplyOption(
    string Title,
    string Payload);
