using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public class ConversationThreadRepository : IConversationThreadRepository
{
    private readonly PlatformDbContext _context;

    public ConversationThreadRepository(PlatformDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ConversationThread> GetOrCreateAsync(
        Guid TenantId,
        string platform,
        string customerIdentifier,
        string businessIdentifier,
        string? customerDisplayName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPlatform = platform.Trim().ToLowerInvariant();
        var normalizedCustomer = customerIdentifier.Trim();
        var normalizedBusiness = businessIdentifier.Trim();

        var existing = await _context.Set<ConversationThread>()
            .FirstOrDefaultAsync(
                x => x.TenantId == TenantId &&
                     x.Platform == normalizedPlatform &&
                     x.CustomerIdentifier == normalizedCustomer &&
                     x.BusinessIdentifier == normalizedBusiness,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var thread = ConversationThread.Create(
            TenantId,
            normalizedPlatform,
            normalizedCustomer,
            normalizedBusiness,
            customerDisplayName);

        await _context.Set<ConversationThread>().AddAsync(thread, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return thread;
    }

    public async Task<ConversationThread?> GetByIdAsync(Guid TenantId, Guid conversationThreadId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ConversationThread>()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == conversationThreadId, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationThread>> ListRecentAsync(
        Guid TenantId,
        int pageIndex = 0,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _context.Set<ConversationThread>()
            .Where(x => x.TenantId == TenantId).AsNoTracking()
            .OrderByDescending(x => x.LastMessageAt ?? x.CreatedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return data.AsReadOnly();
    }

    public async Task UpdateAsync(ConversationThread thread, CancellationToken cancellationToken = default)
    {
        _context.Set<ConversationThread>().Update(thread);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveThreadWithMessagesAsync(
        ConversationThread thread,
        IReadOnlyCollection<Message> messages,
        CancellationToken cancellationToken = default)
    {
        _context.Set<ConversationThread>().Update(thread);

        if (messages.Count > 0)
        {
            // These are new message aggregates; use AddRange to avoid concurrency conflicts.
            _context.Set<Message>().AddRange(messages);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

