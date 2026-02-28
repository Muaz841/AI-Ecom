using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly PlatformDbContext _context;

    public MessageRepository(PlatformDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Message message, CancellationToken cancellationToken = default)
    {
        _context.Messages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Message?> GetByIdAsync(Guid clientId, Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ClientId == clientId, cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetRecentUnprocessedAsync(
        Guid clientId,
        int maxCount = 50,
        TimeSpan? withinLast = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Messages
            .Where(m => m.ClientId == clientId && !m.IsHandledByAI);

        if (withinLast.HasValue)
        {
            var cutoff = DateTime.UtcNow - withinLast.Value;
            query = query.Where(m => m.ReceivedAt >= cutoff);
        }

        var results = await query
            .OrderByDescending(m => m.ReceivedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return results.AsReadOnly();
    }

    public async Task<bool> ExistsAsync(Guid clientId, string externalMessageId, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .AnyAsync(
                m => m.ClientId == clientId
                     && ((m.ExternalMessageId != null && m.ExternalMessageId == externalMessageId)
                         || (m.RawPayloadJson != null && EF.Functions.Like(m.RawPayloadJson, $"%\"id\":\"{externalMessageId}\"%"))),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetByConversationThreadAsync(
        Guid clientId,
        Guid conversationThreadId,
        int pageIndex = 0,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var messages = await _context.Messages
            .Where(m => m.ClientId == clientId && m.ConversationThreadId == conversationThreadId)
            .OrderBy(m => m.ReceivedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return messages.AsReadOnly();
    }
}
