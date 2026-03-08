using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class RbacService : IRbacService
{
    private readonly PlatformDbContext _dbContext;

    public RbacService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(Guid TenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Permission>()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .Select(x => new PermissionDto(x.Id, x.Name, x.Code, x.Description, x.IsSystem))
            .ToListAsync(cancellationToken);
    }

    public async Task<PermissionDto> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateClientId(request.TenantId);
        ValidateNameCode(request.Name, request.Code);

        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        var exists = await _dbContext.Set<Permission>()
            .AnyAsync(x => x.TenantId == request.TenantId && x.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        var entity = Permission.Create(request.TenantId, request.Name, request.Code, request.Description);
        await _dbContext.Set<Permission>().AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PermissionDto(entity.Id, entity.Name, entity.Code, entity.Description, entity.IsSystem);
    }

    public async Task<PermissionDto?> UpdatePermissionAsync(UpdatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateClientId(request.TenantId);
        ValidateNameCode(request.Name, request.Code);

        var entity = await _dbContext.Set<Permission>()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Id == request.PermissionId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System permission cannot be modified.");
        }

        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        var duplicate = await _dbContext.Set<Permission>()
            .AnyAsync(x => x.TenantId == request.TenantId && x.Id != request.PermissionId && x.Code == normalizedCode, cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        entity.Update(request.Name, request.Code, request.Description);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PermissionDto(entity.Id, entity.Name, entity.Code, entity.Description, entity.IsSystem);
    }

    public async Task<bool> DeletePermissionAsync(Guid TenantId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        ValidateClientId(TenantId);
        var entity = await _dbContext.Set<Permission>()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == permissionId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System permission cannot be deleted.");
        }

        _dbContext.Set<Permission>().Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(Guid TenantId, CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.Set<Role>()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(x => x.Id).ToHashSet();
        var mappings = await _dbContext.Set<RolePermission>()
            .Where(x => x.TenantId == TenantId && roleIds.Contains(x.RoleId))
            .ToListAsync(cancellationToken);

        var permissionIds = mappings.Select(x => x.PermissionId).ToHashSet();
        var permissions = await _dbContext.Set<Permission>()
            .Where(x => x.TenantId == TenantId && permissionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return roles.Select(role =>
        {
            var rolePermissions = mappings
                .Where(x => x.RoleId == role.Id)
                .Select(x => permissions[x.PermissionId])
                .Select(p => new PermissionDto(p.Id, p.Name, p.Code, p.Description, p.IsSystem))
                .ToList();

            return new RoleDto(role.Id, role.Name, role.Code, role.Description, role.IsSystem, rolePermissions);
        }).ToList();
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ValidateClientId(request.TenantId);
        ValidateNameCode(request.Name, request.Code);

        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        var exists = await _dbContext.Set<Role>()
            .AnyAsync(x => x.TenantId == request.TenantId && x.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Role code already exists.");
        }

        var entity = Role.Create(request.TenantId, request.Name, request.Code, request.Description);
        await _dbContext.Set<Role>().AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RoleDto(entity.Id, entity.Name, entity.Code, entity.Description, entity.IsSystem, Array.Empty<PermissionDto>());
    }

    public async Task<RoleDto?> UpdateRoleAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ValidateClientId(request.TenantId);
        ValidateNameCode(request.Name, request.Code);

        var entity = await _dbContext.Set<Role>()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Id == request.RoleId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System role cannot be modified.");
        }

        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        var duplicate = await _dbContext.Set<Role>()
            .AnyAsync(x => x.TenantId == request.TenantId && x.Id != request.RoleId && x.Code == normalizedCode, cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException("Role code already exists.");
        }

        entity.Update(request.Name, request.Code, request.Description);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new RoleDto(entity.Id, entity.Name, entity.Code, entity.Description, entity.IsSystem, Array.Empty<PermissionDto>());
    }

    public async Task<bool> DeleteRoleAsync(Guid TenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        ValidateClientId(TenantId);
        var entity = await _dbContext.Set<Role>()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == roleId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        if (entity.IsSystem)
        {
            throw new InvalidOperationException("System role cannot be deleted.");
        }

        _dbContext.Set<Role>().Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RoleDto?> SetRolePermissionsAsync(Guid TenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        ValidateClientId(TenantId);

        var role = await _dbContext.Set<Role>()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == roleId, cancellationToken);

        if (role is null)
        {
            return null;
        }

        var distinctPermissionIds = permissionIds.Distinct().ToList();
        var permissions = await _dbContext.Set<Permission>()
            .Where(x => x.TenantId == TenantId && distinctPermissionIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (permissions.Count != distinctPermissionIds.Count)
        {
            throw new InvalidOperationException("One or more permissions are invalid for this tenant.");
        }

        var existingMappings = await _dbContext.Set<RolePermission>()
            .Where(x => x.TenantId == TenantId && x.RoleId == roleId)
            .ToListAsync(cancellationToken);

        _dbContext.Set<RolePermission>().RemoveRange(existingMappings);
        var newMappings = distinctPermissionIds
            .Select(permissionId => RolePermission.Create(TenantId, roleId, permissionId))
            .ToList();

        await _dbContext.Set<RolePermission>().AddRangeAsync(newMappings, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RoleDto(
            role.Id,
            role.Name,
            role.Code,
            role.Description,
            role.IsSystem,
            permissions.Select(p => new PermissionDto(p.Id, p.Name, p.Code, p.Description, p.IsSystem)).ToList());
    }

    public async Task<bool> AssignRoleToUserAsync(Guid TenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        ValidateClientId(TenantId);

        var userExists = await _dbContext.Set<UserAccount>()
            .AnyAsync(x => x.TenantId == TenantId && x.Id == userId, cancellationToken);
        var roleExists = await _dbContext.Set<Role>()
            .AnyAsync(x => x.TenantId == TenantId && x.Id == roleId, cancellationToken);

        if (!userExists || !roleExists)
        {
            return false;
        }

        var alreadyAssigned = await _dbContext.Set<UserRole>()
            .AnyAsync(x => x.TenantId == TenantId && x.UserAccountId == userId && x.RoleId == roleId, cancellationToken);

        if (alreadyAssigned)
        {
            return true;
        }

        await _dbContext.Set<UserRole>().AddAsync(UserRole.Create(TenantId, userId, roleId), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveRoleFromUserAsync(Guid TenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        ValidateClientId(TenantId);
        var mapping = await _dbContext.Set<UserRole>()
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.UserAccountId == userId && x.RoleId == roleId, cancellationToken);

        if (mapping is null)
        {
            return false;
        }

        _dbContext.Set<UserRole>().Remove(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateClientId(Guid TenantId)
    {
        if (TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required.");
        }
    }

    private static void ValidateNameCode(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Name and code are required.");
        }
    }
}

