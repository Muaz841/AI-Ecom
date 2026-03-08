using System;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record ProcessIncomingMessageCommand(
    Guid TenantId,
    string Platform,
    string From,
    string To,
    string Content,
    string? RawPayloadJson = null,
    string? ExternalMessageId = null
) : IRequest<ProcessIncomingMessageResult>;

public record ProcessIncomingMessageResult(
    bool Success,
    string? ReplySent,
    string? DetectedIntent,
    Guid? CreatedMessageId,
    string? ErrorMessage = null
);

