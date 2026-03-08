using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid TenantId, Guid messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetRecentUnprocessedAsync(
        Guid TenantId,
        int maxCount = 50,
        TimeSpan? withinLast = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid TenantId, string externalMessageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetByConversationThreadAsync(
        Guid TenantId,
        Guid conversationThreadId,
        int pageIndex = 0,
        int pageSize = 200,
        CancellationToken cancellationToken = default);
}

