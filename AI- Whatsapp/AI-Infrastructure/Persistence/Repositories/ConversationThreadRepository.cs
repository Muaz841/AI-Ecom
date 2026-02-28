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
        Guid clientId,
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
                x => x.ClientId == clientId &&
                     x.Platform == normalizedPlatform &&
                     x.CustomerIdentifier == normalizedCustomer &&
                     x.BusinessIdentifier == normalizedBusiness,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var thread = ConversationThread.Create(
            clientId,
            normalizedPlatform,
            normalizedCustomer,
            normalizedBusiness,
            customerDisplayName);

        await _context.Set<ConversationThread>().AddAsync(thread, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return thread;
    }

    public async Task<ConversationThread?> GetByIdAsync(Guid clientId, Guid conversationThreadId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ConversationThread>()
            .FirstOrDefaultAsync(x => x.ClientId == clientId && x.Id == conversationThreadId, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationThread>> ListRecentAsync(
        Guid clientId,
        int pageIndex = 0,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _context.Set<ConversationThread>()
            .Where(x => x.ClientId == clientId)
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
}
