using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IRealtimeNotifier
{
    Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default);
}
