using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IRealtimeNotifier
{
    /// <summary>Broadcasts to every authenticated user in the tenant.</summary>
    Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts only to users in the tenant who hold the
    /// <c>conversations.read</c> permission (the messaging subgroup).
    /// </summary>
    Task PublishToMessagingAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default);
}
