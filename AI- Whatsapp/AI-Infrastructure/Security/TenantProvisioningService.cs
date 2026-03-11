using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenantEntity = EcomAI.Platform.Business.Entities.Tenant;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    // Default roles seeded for every new tenant
    private static readonly (string Name, string Code, string Description, string[] PermissionCodes)[] DefaultTenantRoles =
    {
        (
            "Tenant Admin",
            "tenant_admin",
            "Full access to all tenant features",
            PermissionCodes.TenantScoped
        ),
        (
            "Manager",
            "manager",
            "Manage conversations, products and view settings",
            new[]
            {
                PermissionCodes.ConversationsRead,
                PermissionCodes.ConversationsManage,
                PermissionCodes.ProductsManage,
                PermissionCodes.IntegrationsRead,
                PermissionCodes.LogsRead
            }
        ),
        (
            "Agent",
            "agent",
            "View and respond to conversations",
            new[]
            {
                PermissionCodes.ConversationsRead,
                PermissionCodes.ConversationsManage
            }
        )
    };

    private readonly PlatformDbContext _dbContext;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;

    public TenantProvisioningService(PlatformDbContext dbContext, IPasswordHasher<UserAccount> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<TenantProvisionResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        // Check name uniqueness
        var nameExists = await _dbContext.Set<TenantEntity>()
            .AnyAsync(x => x.Name.ToLower() == request.Name.Trim().ToLower(), cancellationToken);
        if (nameExists)
        {
            return new TenantProvisionResult(false, ErrorMessage: "Tenant name already exists.");
        }

        // Check admin email uniqueness within new tenant scope (pre-check using normalized email)
        var normalizedAdminEmail = request.AdminEmail.Trim().ToUpperInvariant();

        // --- Create tenant ---
        var tenant = TenantEntity.Create(request.Name.Trim(), request.BusinessName.Trim());
        await _dbContext.Set<TenantEntity>().AddAsync(tenant, cancellationToken);

        // Create empty ClientSecrets for tenant
        await _dbContext.Set<ClientSecrets>().AddAsync(ClientSecrets.CreateForTenant(tenant.Id), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // --- Seed tenant roles with global permissions ---
        var globalPermissions = await _dbContext.Set<Permission>()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);

        var permissionsByCode = globalPermissions.ToDictionary(p => p.Code, p => p.Id);

        foreach (var (name, code, description, permCodes) in DefaultTenantRoles)
        {
            var role = Role.Create(tenant.Id, name, code, description, isSystem: true);
            await _dbContext.Set<Role>().AddAsync(role, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var roleMappings = permCodes
                .Where(pc => permissionsByCode.ContainsKey(pc))
                .Select(pc => RolePermission.Create(tenant.Id, role.Id, permissionsByCode[pc]))
                .ToList();

            await _dbContext.Set<RolePermission>().AddRangeAsync(roleMappings, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // --- Create tenant admin user ---
        var adminRole = await _dbContext.Set<Role>()
            .FirstAsync(x => x.TenantId == tenant.Id && x.Code == "tenant_admin", cancellationToken);

        var adminUser = UserAccount.Create(
            tenant.Id,
            request.AdminEmail.Trim(),
            "placeholder",
            request.AdminFirstName.Trim(),
            request.AdminLastName.Trim(),
            "tenant_admin");

        adminUser.SetPasswordHash(_passwordHasher.HashPassword(adminUser, request.AdminPassword));
        await _dbContext.Set<UserAccount>().AddAsync(adminUser, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.Set<UserRole>().AddAsync(
            UserRole.Create(tenant.Id, adminUser.Id, adminRole.Id),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TenantProvisionResult(true, TenantId: tenant.Id, AdminUserId: adminUser.Id.ToString());
    }

    public async Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _dbContext.Set<TenantEntity>()
            .Where(x => !x.IsHost)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(t => t.Id).ToList();

        var userCounts = await _dbContext.Set<UserAccount>()
            .Where(x => x.TenantId != null && tenantIds.Contains(x.TenantId!.Value))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = userCounts.ToDictionary(x => x.TenantId!.Value, x => x.Count);

        return tenants.Select(t => new TenantSummaryDto(
            t.Id,
            t.Name,
            t.BusinessName,
            t.IsActive,
            t.CreatedAt,
            countMap.GetValueOrDefault(t.Id, 0)
        )).ToList();
    }

    public async Task<TenantDetailDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Set<TenantEntity>()
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsHost, cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        var users = await _dbContext.Set<UserAccount>()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.BusinessName,
            tenant.IsActive,
            tenant.CreatedAt,
            users.Select(u => new TenantUserDto(
                u.Id, u.Email, u.FirstName, u.LastName, u.Role, u.IsActive, u.CreatedAt
            )).ToList());
    }

    public async Task<bool> SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Set<TenantEntity>()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return false;
        }

        tenant.Suspend();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Set<TenantEntity>()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return false;
        }

        tenant.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateRequest(CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Tenant name is required.");
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            throw new ArgumentException("Business name is required.");
        if (string.IsNullOrWhiteSpace(request.AdminEmail) || !request.AdminEmail.Contains('@'))
            throw new ArgumentException("Valid admin email is required.");
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 8)
            throw new ArgumentException("Admin password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(request.AdminFirstName))
            throw new ArgumentException("Admin first name is required.");
        if (string.IsNullOrWhiteSpace(request.AdminLastName))
            throw new ArgumentException("Admin last name is required.");
    }
}
