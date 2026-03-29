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
        var envelope = BuildEnvelope(eventType, payload);
        return _hubContext.Clients
            .Group(RealtimeHub.BuildTenantGroup(tenantId.ToString()))
            .SendAsync("notification", envelope, cancellationToken);
    }

    public Task PublishToMessagingAsync(Guid tenantId, string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var envelope = BuildEnvelope(eventType, payload);
        return _hubContext.Clients
            .Group(RealtimeHub.BuildMessagingGroup(tenantId.ToString()))
            .SendAsync("notification", envelope, cancellationToken);
    }

    private static RealtimeNotification BuildEnvelope(string eventType, object payload) =>
        new(SchemaVersion: RealtimeNotification.CurrentSchemaVersion,
            EventType:     eventType,
            Payload:       payload,
            CreatedAtUtc:  DateTime.UtcNow);
}

public sealed record RealtimeNotification(
    int SchemaVersion,
    string EventType,
    object Payload,
    DateTime CreatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;
}
