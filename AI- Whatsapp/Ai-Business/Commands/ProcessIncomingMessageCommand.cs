using System;
using System.Collections.Generic;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record ProcessIncomingMessageCommand(
    Guid TenantId,
    string Platform,
    string From,
    string To,
    string Content,
    string? RawPayloadJson = null,
    string? ExternalMessageId = null,
    MessageType MessageType = MessageType.Text,
    bool AllowAutoReply = true
) : IRequest<ProcessIncomingMessageResult>;

public record ProcessIncomingMessageResult(
    bool Success,
    string? ReplySent,
    string? DetectedIntent,
    Guid? CreatedMessageId,
    string? ErrorMessage = null,
    IReadOnlyList<string>? ToolCallsMade = null,
    int InputTokens = 0,
    int OutputTokens = 0);

