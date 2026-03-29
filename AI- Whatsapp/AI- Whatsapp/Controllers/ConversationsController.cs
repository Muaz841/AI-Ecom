using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionCodes.ConversationsRead)]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationThreadRepository _conversationThreadRepository;
    private readonly IMessageRepository _messageRepository;

    public ConversationsController(
        IConversationThreadRepository conversationThreadRepository,
        IMessageRepository messageRepository)
    {
        _conversationThreadRepository = conversationThreadRepository;
        _messageRepository = messageRepository;
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List conversations", Description = "Returns paginated conversation threads for a tenant/client.")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationThreadDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<ConversationThreadDto>>> List(
        [FromQuery] ListConversationsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var threads = await _conversationThreadRepository.ListRecentAsync(
            request.TenantId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(threads.Select(ToDto).ToList());
    }

    [HttpGet("{conversationThreadId:guid}")]
    [SwaggerOperation(Summary = "Get conversation thread", Description = "Returns a single conversation thread by ID.")]
    [ProducesResponseType(typeof(ConversationThreadDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ConversationThreadDto>> GetById(
        Guid conversationThreadId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _conversationThreadRepository.GetByIdAsync(tenantId, conversationThreadId, cancellationToken);
        return thread is null ? NotFound() : Ok(ToDto(thread));
    }

    [HttpGet("{conversationThreadId:guid}/messages")]
    [SwaggerOperation(Summary = "Get conversation messages", Description = "Returns message timeline for a conversation thread.")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationMessageDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<ConversationMessageDto>>> GetMessages(
        Guid conversationThreadId,
        [FromQuery] ConversationMessagesQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var thread = await _conversationThreadRepository.GetByIdAsync(request.TenantId, conversationThreadId, cancellationToken);
        if (thread is null)
        {
            return NotFound();
        }

        var messages = await _messageRepository.GetByConversationThreadAsync(
            request.TenantId,
            conversationThreadId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(messages.Select(ToDto).ToList());
    }

    private static ConversationThreadDto ToDto(ConversationThread thread)
    {
        return new ConversationThreadDto(
            thread.Id,
            thread.TenantId ?? Guid.Empty,
            thread.Platform,
            thread.CustomerIdentifier,
            thread.BusinessIdentifier,
            thread.CustomerDisplayName,
            thread.LastMessagePreview,
            thread.LastMessageDirection?.ToString().ToLowerInvariant(),
            thread.LastMessageAt,
            thread.MessageCount,
            thread.IsOpen,
            thread.AssignmentMode.ToString().ToLowerInvariant());
    }

    private static ConversationMessageDto ToDto(Message message)
    {
        return new ConversationMessageDto(
            message.Id,
            message.ConversationThreadId,
            message.Platform,
            message.Direction.ToString().ToLowerInvariant(),
            message.MessageType.ToString().ToLowerInvariant(),
            message.From,
            message.To,
            message.Content,
            message.ExternalMessageId,
            message.DeliveryStatus?.ToString().ToLowerInvariant(),
            message.ReceivedAt,
            message.SentAt);
    }
}

public record ConversationThreadDto(
    Guid Id,
    Guid TenantId,
    string Platform,
    string CustomerIdentifier,
    string BusinessIdentifier,
    string? CustomerDisplayName,
    string? LastMessagePreview,
    string? LastMessageDirection,
    DateTime? LastMessageAt,
    int MessageCount,
    bool IsOpen,
    string AssignmentMode);

public record ConversationMessageDto(
    Guid Id,
    Guid? ConversationThreadId,
    string Platform,
    string Direction,
    string MessageType,
    string From,
    string To,
    string Content,
    string? ExternalMessageId,
    string? DeliveryStatus,
    DateTime ReceivedAt,
    DateTime? SentAt);

public sealed record ListConversationsQueryRequest(
    Guid TenantId,
    int PageIndex = 0,
    int PageSize = 50);

public sealed record ConversationMessagesQueryRequest(
    Guid TenantId,
    int PageIndex = 0,
    int PageSize = 200);

