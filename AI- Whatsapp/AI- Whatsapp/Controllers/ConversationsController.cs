using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize]
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
        [FromQuery] Guid clientId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (clientId == Guid.Empty)
        {
            return BadRequest("clientId is required.");
        }

        if (pageIndex < 0 || pageSize <= 0 || pageSize > 200)
        {
            return BadRequest("Invalid paging parameters.");
        }

        var threads = await _conversationThreadRepository.ListRecentAsync(clientId, pageIndex, pageSize, cancellationToken);
        return Ok(threads.Select(ToDto).ToList());
    }

    [HttpGet("{conversationThreadId:guid}/messages")]
    [SwaggerOperation(Summary = "Get conversation messages", Description = "Returns message timeline for a conversation thread.")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationMessageDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<ConversationMessageDto>>> GetMessages(
        Guid conversationThreadId,
        [FromQuery] Guid clientId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        if (clientId == Guid.Empty)
        {
            return BadRequest("clientId is required.");
        }

        var thread = await _conversationThreadRepository.GetByIdAsync(clientId, conversationThreadId, cancellationToken);
        if (thread is null)
        {
            return NotFound();
        }

        var messages = await _messageRepository.GetByConversationThreadAsync(clientId, conversationThreadId, pageIndex, pageSize, cancellationToken);
        return Ok(messages.Select(ToDto).ToList());
    }

    private static ConversationThreadDto ToDto(ConversationThread thread)
    {
        return new ConversationThreadDto(
            thread.Id,
            thread.ClientId,
            thread.Platform,
            thread.CustomerIdentifier,
            thread.BusinessIdentifier,
            thread.CustomerDisplayName,
            thread.LastMessagePreview,
            thread.LastMessageDirection,
            thread.LastMessageAt,
            thread.MessageCount,
            thread.IsOpen,
            thread.AssignmentMode);
    }

    private static ConversationMessageDto ToDto(Message message)
    {
        return new ConversationMessageDto(
            message.Id,
            message.ConversationThreadId,
            message.Platform,
            message.Direction,
            message.MessageType,
            message.From,
            message.To,
            message.Content,
            message.ExternalMessageId,
            message.DeliveryStatus,
            message.ReceivedAt,
            message.SentAt);
    }
}

public record ConversationThreadDto(
    Guid Id,
    Guid ClientId,
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
