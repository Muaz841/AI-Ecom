using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class RbacSeedHostedService : IHostedService
{
    private static readonly (string Name, string Code, string Description)[] DefaultPermissions =
    {
        ("Manage Users", "users.manage", "Create/update/deactivate users"),
        ("Manage Roles", "roles.manage", "Create/update/delete roles"),
        ("Manage Permissions", "permissions.manage", "Create/update/delete permissions and role permissions"),
        ("Manage Clients", "clients.manage", "Manage tenant client settings"),
        ("Manage Products", "products.manage", "Create/update/delete products"),
        ("View Conversations", "conversations.read", "Read inbox and message timelines"),
        ("Manage Conversations", "conversations.manage", "Respond/assign/close conversations"),
        ("View Logs", "logs.read", "Read integration and application logs"),
        ("Manage AI", "ai.manage", "Configure AI provider and policies"),
        ("Manage Webhooks", "webhooks.manage", "Manage webhook and integration settings")
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

        var hostClient = await ResolveHostClientAsync(db, cancellationToken);
        if (hostClient is null)
        {
            logger.Warning("RBAC seeding skipped. No host client available.");
            return;
        }

        foreach (var permission in DefaultPermissions)
        {
            var exists = await db.Set<Permission>()
                .AnyAsync(x => x.ClientId == hostClient.Id && x.Code == permission.Code, cancellationToken);
            if (!exists)
            {
                await db.Set<Permission>().AddAsync(
                    Permission.Create(hostClient.Id, permission.Name, permission.Code, permission.Description, isSystem: true),
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var allPermissions = await db.Set<Permission>()
            .Where(x => x.ClientId == hostClient.Id)
            .ToListAsync(cancellationToken);

        var superAdminRole = await db.Set<Role>()
            .FirstOrDefaultAsync(x => x.ClientId == hostClient.Id && x.Code == "super_admin", cancellationToken);

        if (superAdminRole is null)
        {
            superAdminRole = Role.Create(hostClient.Id, "Super Admin", "super_admin", "Host super user role", isSystem: true);
            await db.Set<Role>().AddAsync(superAdminRole, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingRolePermissions = await db.Set<RolePermission>()
            .Where(x => x.ClientId == hostClient.Id && x.RoleId == superAdminRole.Id)
            .ToListAsync(cancellationToken);

        db.Set<RolePermission>().RemoveRange(existingRolePermissions);
        await db.Set<RolePermission>().AddRangeAsync(
            allPermissions.Select(p => RolePermission.Create(hostClient.Id, superAdminRole.Id, p.Id)),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(_settings.SuperUserEmail) || string.IsNullOrWhiteSpace(_settings.SuperUserPassword))
        {
            logger.Warning("Super user credentials are not configured. Role/permission seed completed without super user account.");
            return;
        }

        var normalizedEmail = _settings.SuperUserEmail.Trim().ToUpperInvariant();
        var user = await db.Set<UserAccount>()
            .FirstOrDefaultAsync(x => x.ClientId == hostClient.Id && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = UserAccount.Create(
                hostClient.Id,
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
            x => x.ClientId == hostClient.Id && x.UserAccountId == user.Id && x.RoleId == superAdminRole.Id,
            cancellationToken);

        if (!assigned)
        {
            await db.Set<UserRole>().AddAsync(UserRole.Create(hostClient.Id, user.Id, superAdminRole.Id), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.Info("RBAC seeding completed. Host client {ClientId}, super user {Email}.", hostClient.Id, user.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<Client?> ResolveHostClientAsync(Persistence.PlatformDbContext db, CancellationToken cancellationToken)
    {
        if (_settings.HostClientId.HasValue && _settings.HostClientId.Value != Guid.Empty)
        {
            var configured = await db.Set<Client>().FirstOrDefaultAsync(x => x.Id == _settings.HostClientId.Value, cancellationToken);
            if (configured is not null)
            {
                return configured;
            }
        }

        if (_settings.CreateHostClientIfMissing)
        {
            var host = Client.Create(
                name: _settings.HostClientName ?? "host-tenant",
                businessName: _settings.HostBusinessName ?? "Host Tenant",
                metaAccessToken: "seed-placeholder-token",
                metaPageId: "seed-placeholder-page",
                whatsAppBusinessAccountId: "seed-placeholder-waba");

            await db.Set<Client>().AddAsync(host, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return host;
        }

        return await db.Set<Client>().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed class BootstrapSettings
{
    public bool EnableBootstrapSeeding { get; set; } = true;
    public Guid? HostClientId { get; set; }
    public bool CreateHostClientIfMissing { get; set; } = false;
    public string? HostClientName { get; set; }
    public string? HostBusinessName { get; set; }
    public string? SuperUserEmail { get; set; }
    public string? SuperUserPassword { get; set; }
    public string? SuperUserFirstName { get; set; }
    public string? SuperUserLastName { get; set; }
}
