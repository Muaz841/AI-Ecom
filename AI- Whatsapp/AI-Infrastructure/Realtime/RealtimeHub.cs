using System.Linq;
using System.Security.Claims;
using EcomAI.Platform.Business.Security;
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
            // All authenticated tenant users join the broad group.
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildTenantGroup(tenantId));

            // Users with conversations.read join the messaging subgroup.
            // This keeps message events scoped to agents and admins only.
            if (HasPermission(PermissionCodes.ConversationsRead))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, BuildMessagingGroup(tenantId));
            }
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

            if (HasPermission(PermissionCodes.ConversationsRead))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildMessagingGroup(tenantId));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    internal static string BuildTenantGroup(string tenantId)    => $"tenant:{tenantId}";
    internal static string BuildMessagingGroup(string tenantId) => $"tenant:{tenantId}:messaging";

    private bool HasPermission(string code) =>
        Context.User?.Claims.Any(c => c.Type == "permission" && c.Value == code) == true;
}
