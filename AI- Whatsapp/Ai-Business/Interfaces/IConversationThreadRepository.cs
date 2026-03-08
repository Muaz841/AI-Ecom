using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IConversationThreadRepository
{
    Task<ConversationThread> GetOrCreateAsync(
        Guid TenantId,
        string platform,
        string customerIdentifier,
        string businessIdentifier,
        string? customerDisplayName = null,
        CancellationToken cancellationToken = default);

    Task<ConversationThread?> GetByIdAsync(
        Guid TenantId,
        Guid conversationThreadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationThread>> ListRecentAsync(
        Guid TenantId,
        int pageIndex = 0,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(ConversationThread thread, CancellationToken cancellationToken = default);

    Task SaveThreadWithMessagesAsync(
        ConversationThread thread,
        IReadOnlyCollection<Message> messages,
        CancellationToken cancellationToken = default);
}

