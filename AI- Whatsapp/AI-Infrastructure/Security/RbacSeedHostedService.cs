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
    private static readonly (string Name, string Code, string Description)[] DefaultPermissions =
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
        ("Manage Integrations", PermissionCodes.IntegrationsManage, "Connect/disconnect Meta channel integrations")
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

        var hostTenant = await ResolveHostTenantAsync(db, cancellationToken);
        if (hostTenant is null)
        {
            logger.Warning("RBAC seeding skipped. No host tenant available.");
            return;
        }

        foreach (var permission in DefaultPermissions)
        {
            var exists = await db.Set<Permission>()
                .AnyAsync(x => x.TenantId == hostTenant.Id && x.Code == permission.Code, cancellationToken);
            if (!exists)
            {
                await db.Set<Permission>().AddAsync(
                    Permission.Create(hostTenant.Id, permission.Name, permission.Code, permission.Description, isSystem: true),
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var allPermissions = await db.Set<Permission>()
            .Where(x => x.TenantId == hostTenant.Id)
            .ToListAsync(cancellationToken);

        var superAdminRole = await db.Set<Role>()
            .FirstOrDefaultAsync(x => x.TenantId == hostTenant.Id && x.Code == "super_admin", cancellationToken);

        if (superAdminRole is null)
        {
            superAdminRole = Role.Create(hostTenant.Id, "Super Admin", "super_admin", "Host super user role", isSystem: true);
            await db.Set<Role>().AddAsync(superAdminRole, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingRolePermissions = await db.Set<RolePermission>()
            .Where(x => x.TenantId == hostTenant.Id && x.RoleId == superAdminRole.Id)
            .ToListAsync(cancellationToken);

        db.Set<RolePermission>().RemoveRange(existingRolePermissions);
        await db.Set<RolePermission>().AddRangeAsync(
            allPermissions.Select(p => RolePermission.Create(hostTenant.Id, superAdminRole.Id, p.Id)),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_settings.SuperUserEmail) || string.IsNullOrWhiteSpace(_settings.SuperUserPassword))
        {
            logger.Warning("Super user credentials are not configured. Role/permission seed completed without super user account.");
            return;
        }

        var normalizedEmail = _settings.SuperUserEmail.Trim().ToUpperInvariant();
        var user = await db.Set<UserAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == hostTenant.Id && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = UserAccount.Create(
                hostTenant.Id,
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
            x => x.TenantId == hostTenant.Id && x.UserAccountId == user.Id && x.RoleId == superAdminRole.Id,
            cancellationToken);

        if (!assigned)
        {
            await db.Set<UserRole>().AddAsync(UserRole.Create(hostTenant.Id, user.Id, superAdminRole.Id), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Info("RBAC seeding completed. Host tenant {TenantId}, super user {Email}.", hostTenant.Id, user.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<TenantEntity?> ResolveHostTenantAsync(Persistence.PlatformDbContext db, CancellationToken cancellationToken)
    {
        if (_settings.HostTenantId.HasValue && _settings.HostTenantId.Value != Guid.Empty)
        {
            var configured = await db.Set<TenantEntity>().FirstOrDefaultAsync(x => x.Id == _settings.HostTenantId.Value, cancellationToken);
            if (configured is not null)
            {
                return configured;
            }
        }

        var hostName = (_settings.HostTenantName ?? "host-tenant").Trim();
        var hostBusinessName = (_settings.HostBusinessName ?? "Host Tenant").Trim();

        var hostByName = await db.Set<TenantEntity>()
            .FirstOrDefaultAsync(
                x => x.Name.ToLower() == hostName.ToLower()
                  || x.BusinessName.ToLower() == hostBusinessName.ToLower(),
                cancellationToken);

        if (hostByName is not null)
        {
            return hostByName;
        }

        var firstExisting = await db.Set<TenantEntity>().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (firstExisting is not null)
        {
            return firstExisting;
        }

        if (_settings.CreateHostTenantIfMissing)
        {
            var host = TenantEntity.Create(
                name: hostName,
                businessName: hostBusinessName);

            await db.Set<TenantEntity>().AddAsync(host, cancellationToken);
            await db.Set<ClientSecrets>().AddAsync(ClientSecrets.CreateForTenant(host.Id), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return host;
        }

        // Safety fallback for first startup when host creation flag is disabled.
        var fallbackHost = TenantEntity.Create(
            name: hostName,
            businessName: hostBusinessName);

        await db.Set<TenantEntity>().AddAsync(fallbackHost, cancellationToken);
        await db.Set<ClientSecrets>().AddAsync(ClientSecrets.CreateForTenant(fallbackHost.Id), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return fallbackHost;
    }
}

public sealed class BootstrapSettings
{
    public bool EnableBootstrapSeeding { get; set; } = true;
    public Guid? HostTenantId { get; set; }
    public bool CreateHostTenantIfMissing { get; set; } = false;
    public string? HostTenantName { get; set; }
    public string? HostBusinessName { get; set; }
    public string? SuperUserEmail { get; set; }
    public string? SuperUserPassword { get; set; }
    public string? SuperUserFirstName { get; set; }
    public string? SuperUserLastName { get; set; }
}

