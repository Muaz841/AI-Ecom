using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EcomAI.Platform.Infrastructure.Realtime;

public sealed class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<RealtimeHub> _hubContext;

    public SignalRNotifier(IHubContext<RealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var envelope = new RealtimeNotification(
            EventType: eventType,
            Payload: payload,
            CreatedAtUtc: DateTime.UtcNow);

        return _hubContext
            .Clients
            .Group(RealtimeHub.BuildTenantGroup(tenantId.ToString()))
            .SendAsync("notification", envelope, cancellationToken);
    }
}

public sealed record RealtimeNotification(
    string EventType,
    object Payload,
    DateTime CreatedAtUtc);
