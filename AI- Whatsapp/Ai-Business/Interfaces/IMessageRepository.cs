using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IMessageRepository
{
    Task AddAsync(Message message, CancellationToken cancellationToken = default);

    Task UpdateAsync(Message message, CancellationToken cancellationToken = default);

    Task<Message?> GetByIdAsync(Guid clientId, Guid messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetRecentUnprocessedAsync(
        Guid clientId,
        int maxCount = 50,
        TimeSpan? withinLast = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid clientId, string externalMessageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetByConversationThreadAsync(
        Guid clientId,
        Guid conversationThreadId,
        int pageIndex = 0,
        int pageSize = 200,
        CancellationToken cancellationToken = default);
}
