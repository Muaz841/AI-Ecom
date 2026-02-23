using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IMessageRepository
{
    Task AddAsync(Message message);

    Task UpdateAsync(Message message);

    Task<Message?> GetByIdAsync(Guid clientId, Guid messageId);

    Task<IReadOnlyList<Message>> GetRecentUnprocessedAsync(
        Guid clientId,
        int maxCount = 50,
        TimeSpan? withinLast = null);

    Task<bool> ExistsAsync(Guid clientId, string externalMessageId);
}
