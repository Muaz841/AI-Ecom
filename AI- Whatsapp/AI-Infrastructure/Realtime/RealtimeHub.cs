using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EcomAI.Platform.Infrastructure.Realtime;

[Authorize]
public sealed class RealtimeHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirstValue("tenant_id")
                      ?? Context.User?.FindFirstValue("client_id");

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildTenantGroup(tenantId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirstValue("tenant_id")
                      ?? Context.User?.FindFirstValue("client_id");

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildTenantGroup(tenantId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    internal static string BuildTenantGroup(string tenantId) => $"tenant:{tenantId}";
}
