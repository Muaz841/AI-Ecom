using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TenantEntity = EcomAI.Platform.Business.Entities.Tenant;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class RbacSeedHostedService : IHostedService
{    
    private static readonly (string Name, string Code, string Description)[] AllPermissions =
    {
        ("Manage Users", PermissionCodes.UsersManage, "Create/update/deactivate users"),
        ("Manage Roles", PermissionCodes.RolesManage, "Create/update/delete roles"),
        ("Manage Permissions", PermissionCodes.PermissionsManage, "Create/update/delete permissions and role permissions"),
        ("Manage Clients", PermissionCodes.ClientsManage, "Manage tenant client settings"),
        ("Manage Products", PermissionCodes.ProductsManage, "Create/update/delete products"),
        ("View Conversations", PermissionCodes.ConversationsRead, "Read inbox and message timelines"),
        ("Manage Conversations", PermissionCodes.ConversationsManage, "Respond/assign/close conversations"),
        ("View Logs", PermissionCodes.LogsRead, "Read integration and application logs"),
        ("Manage AI", PermissionCodes.AiManage, "Configure AI provider and policies"),
        ("Manage Webhooks", PermissionCodes.WebhooksManage, "Manage webhook and integration settings"),
        ("View Integrations", PermissionCodes.IntegrationsRead, "Read channel integration connection status"),
        ("Manage Integrations", PermissionCodes.IntegrationsManage, "Connect/disconnect Meta channel integrations"),
        ("Manage Tenants", PermissionCodes.TenantsManage, "Create/suspend/manage platform tenants"),
        ("Manage Subscriptions", PermissionCodes.SubscriptionsManage, "Manage tenant subscriptions and billing"),
        ("Platform Settings", PermissionCodes.PlatformSettings, "Manage global platform configuration")
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BootstrapSettings _settings;

    public RbacSeedHostedService(IServiceScopeFactory scopeFactory, IOptions<BootstrapSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnableBootstrapSeeding)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.PlatformDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<Business.Interfaces.IApplicationLogger>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
              
  
        foreach (var permission in AllPermissions)
        {
            var exists = await db.Set<Permission>()
                .AnyAsync(x => x.TenantId == null && x.Code == permission.Code, cancellationToken);
            if (!exists)
            {
                await db.Set<Permission>().AddAsync(
                    Permission.Create(permission.Name, permission.Code, permission.Description, isSystem: true),
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var allPermissions = await db.Set<Permission>()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        
        var superAdminRole = await db.Set<Role>()
            .FirstOrDefaultAsync(x => x.TenantId == null && x.Code == "super_admin", cancellationToken);

        if (superAdminRole is null)
        {
            superAdminRole = Role.Create(null, "Super Admin", "super_admin", "Host super user role", isSystem: true);
            await db.Set<Role>().AddAsync(superAdminRole, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        
        var existingRolePermissions = await db.Set<RolePermission>()
            .Where(x => x.TenantId == null && x.RoleId == superAdminRole.Id)
            .ToListAsync(cancellationToken);

        db.Set<RolePermission>().RemoveRange(existingRolePermissions);
        await db.Set<RolePermission>().AddRangeAsync(
            allPermissions.Select(p => RolePermission.Create(null, superAdminRole.Id, p.Id)),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Ensure all existing tenant roles have their expected permissions,
        // and all tenant users are assigned to their role.
        // Handles tenants created before permissions were seeded (race condition / re-used DB).
        await EnsureTenantRolePermissionsAsync(db, allPermissions, cancellationToken);
        await EnsureTenantUserRolesAsync(db, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(_settings.SuperUserEmail) || string.IsNullOrWhiteSpace(_settings.SuperUserPassword))
        {
            logger.Warning("Super user credentials are not configured. Role/permission seed completed without super user account.");
            return;
        }

        var normalizedEmail = _settings.SuperUserEmail.Trim().ToUpperInvariant();
        var user = await db.Set<UserAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == null && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = UserAccount.Create(
                null, 
                _settings.SuperUserEmail.Trim(),
                "placeholder",
                _settings.SuperUserFirstName ?? "Host",
                _settings.SuperUserLastName ?? "Admin",
                "super_admin");
            user.SetPasswordHash(hasher.HashPassword(user, _settings.SuperUserPassword));
            await db.Set<UserAccount>().AddAsync(user, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var assigned = await db.Set<UserRole>().AnyAsync(
            x => x.TenantId == null && x.UserAccountId == user.Id && x.RoleId == superAdminRole.Id,
            cancellationToken);

        if (!assigned)
        {
            await db.Set<UserRole>().AddAsync(UserRole.Create(null, user.Id, superAdminRole.Id), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
      
    }

    // Expected permission codes per tenant role code — must stay in sync with TenantProvisioningService.DefaultTenantRoles
    private static readonly Dictionary<string, string[]> TenantRolePermissions = new()
    {
        ["tenant_admin"] = PermissionCodes.TenantScoped,
        ["manager"] = [
            PermissionCodes.ConversationsRead,
            PermissionCodes.ConversationsManage,
            PermissionCodes.ProductsManage,
            PermissionCodes.IntegrationsRead,
            PermissionCodes.LogsRead
        ],
        ["agent"] = [
            PermissionCodes.ConversationsRead,
            PermissionCodes.ConversationsManage
        ]
    };

    private static async Task EnsureTenantRolePermissionsAsync(
        Persistence.PlatformDbContext db,
        System.Collections.Generic.List<Permission> allGlobalPermissions,
        CancellationToken cancellationToken)
    {
        var permissionByCode = allGlobalPermissions.ToDictionary(p => p.Code, p => p.Id);

        // Load all tenant roles matching the default role codes
        var tenantRoles = await db.Set<Role>()
            .Where(r => r.TenantId != null && TenantRolePermissions.Keys.Contains(r.Code))
            .ToListAsync(cancellationToken);

        if (tenantRoles.Count == 0) return;

        var roleIds = tenantRoles.Select(r => r.Id).ToList();

        // Load all existing role permission assignments for these roles
        var existingAssignments = await db.Set<RolePermission>()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(cancellationToken);

        var existingSet = existingAssignments
            .Select(x => (x.RoleId, x.PermissionId))
            .ToHashSet();

        var toAdd = new System.Collections.Generic.List<RolePermission>();

        foreach (var role in tenantRoles)
        {
            if (!TenantRolePermissions.TryGetValue(role.Code, out var expectedCodes)) continue;

            foreach (var code in expectedCodes)
            {
                if (!permissionByCode.TryGetValue(code, out var permId)) continue;
                if (!existingSet.Contains((role.Id, permId)))
                {
                    toAdd.Add(RolePermission.Create(role.TenantId, role.Id, permId));
                }
            }
        }

        if (toAdd.Count > 0)
        {
            await db.Set<RolePermission>().AddRangeAsync(toAdd, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // Ensures every tenant UserAccount is assigned to the Role matching their denormalized Role field.
    // Fixes users created without a corresponding UserRole entry.
    private static async Task EnsureTenantUserRolesAsync(
        Persistence.PlatformDbContext db,
        CancellationToken cancellationToken)
    {
        // Load all tenant roles (code → role)
        var tenantRoles = await db.Set<Role>()
            .Where(r => r.TenantId != null && TenantRolePermissions.Keys.Contains(r.Code))
            .ToListAsync(cancellationToken);

        if (tenantRoles.Count == 0) return;

        // Group roles by (tenantId, code) for fast lookup
        var roleMap = tenantRoles
            .GroupBy(r => (r.TenantId!.Value, r.Code))
            .ToDictionary(g => g.Key, g => g.First().Id);

        var tenantIds = tenantRoles.Select(r => r.TenantId!.Value).Distinct().ToList();

        // Load all tenant users whose denormalized Role is one of the default codes
        var tenantUsers = await db.Set<UserAccount>()
            .Where(u => u.TenantId != null
                && tenantIds.Contains(u.TenantId!.Value)
                && TenantRolePermissions.Keys.Contains(u.Role))
            .ToListAsync(cancellationToken);

        if (tenantUsers.Count == 0) return;

        var userIds = tenantUsers.Select(u => u.Id).ToList();

        // Load existing UserRole assignments for these users
        var existingUserRoles = await db.Set<UserRole>()
            .Where(ur => userIds.Contains(ur.UserAccountId))
            .Select(ur => ur.UserAccountId)
            .ToHashSetAsync(cancellationToken);

        var toAdd = new System.Collections.Generic.List<UserRole>();

        foreach (var user in tenantUsers)
        {
            if (existingUserRoles.Contains(user.Id)) continue;

            var key = (user.TenantId!.Value, user.Role);
            if (!roleMap.TryGetValue(key, out var roleId)) continue;

            toAdd.Add(UserRole.Create(user.TenantId, user.Id, roleId));
        }

        if (toAdd.Count > 0)
        {
            await db.Set<UserRole>().AddRangeAsync(toAdd, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

 
}

public sealed class BootstrapSettings
{
    public bool EnableBootstrapSeeding { get; set; } = true;
    public Guid? HostTenantId { get; set; }
    public string? HostTenantName { get; set; }
    public string? HostBusinessName { get; set; }
    public string? SuperUserEmail { get; set; }
    public string? SuperUserPassword { get; set; }
    public string? SuperUserFirstName { get; set; }
    public string? SuperUserLastName { get; set; }
}
