using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IRbacService
{
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(Guid TenantId, CancellationToken cancellationToken = default);
    Task<PermissionDto> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default);
    Task<PermissionDto?> UpdatePermissionAsync(UpdatePermissionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePermissionAsync(Guid TenantId, Guid permissionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> ListRolesAsync(Guid TenantId, CancellationToken cancellationToken = default);
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleDto?> UpdateRoleAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoleAsync(Guid TenantId, Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleDto?> SetRolePermissionsAsync(Guid TenantId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default);

    Task<bool> AssignRoleToUserAsync(Guid TenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> RemoveRoleFromUserAsync(Guid TenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RbacUserDto>> ListUsersAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record PermissionDto(Guid Id, string Name, string Code, string? Description, bool IsSystem);
public sealed record RoleDto(Guid Id, string Name, string Code, string? Description, bool IsSystem, IReadOnlyList<PermissionDto> Permissions);
public sealed record RbacUserDto(Guid Id, string Email, string FirstName, string LastName, bool IsActive, DateTime CreatedAt, IReadOnlyList<string> Roles);

public sealed record CreatePermissionRequest(Guid TenantId, string Name, string Code, string? Description);
public sealed record UpdatePermissionRequest(Guid TenantId, Guid PermissionId, string Name, string Code, string? Description);
public sealed record CreateRoleRequest(Guid TenantId, string Name, string Code, string? Description);
public sealed record UpdateRoleRequest(Guid TenantId, Guid RoleId, string Name, string Code, string? Description);

