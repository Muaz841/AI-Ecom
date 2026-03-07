using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IRbacService
{
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<PermissionDto> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default);
    Task<PermissionDto?> UpdatePermissionAsync(UpdatePermissionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePermissionAsync(Guid clientId, Guid permissionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> ListRolesAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleDto?> UpdateRoleAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoleAsync(Guid clientId, Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleDto?> SetRolePermissionsAsync(Guid clientId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default);

    Task<bool> AssignRoleToUserAsync(Guid clientId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> RemoveRoleFromUserAsync(Guid clientId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}

public sealed record PermissionDto(Guid Id, string Name, string Code, string? Description, bool IsSystem);
public sealed record RoleDto(Guid Id, string Name, string Code, string? Description, bool IsSystem, IReadOnlyList<PermissionDto> Permissions);

public sealed record CreatePermissionRequest(Guid ClientId, string Name, string Code, string? Description);
public sealed record UpdatePermissionRequest(Guid ClientId, Guid PermissionId, string Name, string Code, string? Description);
public sealed record CreateRoleRequest(Guid ClientId, string Name, string Code, string? Description);
public sealed record UpdateRoleRequest(Guid ClientId, Guid RoleId, string Name, string Code, string? Description);
