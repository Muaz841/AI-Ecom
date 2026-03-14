using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rbac")]
public class RbacController : ControllerBase
{
    private readonly IRbacService _rbacService;

    public RbacController(IRbacService rbacService)
    {
        _rbacService = rbacService;
    }

    [HttpGet("permissions")]
    [Authorize(Policy = PermissionCodes.PermissionsManage)]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> ListPermissions(CancellationToken cancellationToken)
    {
        var result = await _rbacService.ListPermissionsAsync(Guid.Empty, cancellationToken);
        return Ok(result);
    }

    [HttpPost("permissions")]
    [Authorize(Policy = PermissionCodes.PermissionsManage)]
    public async Task<ActionResult<PermissionDto>> CreatePermission([FromBody] CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        var created = await _rbacService.CreatePermissionAsync(request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("permissions/{permissionId:guid}")]
    [Authorize(Policy = PermissionCodes.PermissionsManage)]
    public async Task<ActionResult<PermissionDto>> UpdatePermission(Guid permissionId, [FromBody] UpdatePermissionApiRequest request, CancellationToken cancellationToken)
    {
        var updated = await _rbacService.UpdatePermissionAsync(
            new UpdatePermissionRequest(request.TenantId, permissionId, request.Name, request.Code, request.Description),
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("permissions/{permissionId:guid}")]
    [Authorize(Policy = PermissionCodes.PermissionsManage)]
    public async Task<IActionResult> DeletePermission(Guid permissionId, [FromQuery] TenantScopedQuery request, CancellationToken cancellationToken)
    {
        var deleted = await _rbacService.DeletePermissionAsync(request.TenantId, permissionId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("roles")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles([FromQuery] TenantScopedQuery request, CancellationToken cancellationToken)
    {
        var result = await _rbacService.ListRolesAsync(request.TenantId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("roles")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var created = await _rbacService.CreateRoleAsync(request, cancellationToken);
        return Ok(created);
    }

    [HttpPut("roles/{roleId:guid}")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid roleId, [FromBody] UpdateRoleApiRequest request, CancellationToken cancellationToken)
    {
        var updated = await _rbacService.UpdateRoleAsync(
            new UpdateRoleRequest(request.TenantId, roleId, request.Name, request.Code, request.Description),
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("roles/{roleId:guid}")]
    [Authorize(Policy = PermissionCodes.RolesManage)]
    public async Task<IActionResult> DeleteRole(Guid roleId, [FromQuery] TenantScopedQuery request, CancellationToken cancellationToken)
    {
        var deleted = await _rbacService.DeleteRoleAsync(request.TenantId, roleId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    [Authorize(Policy = PermissionCodes.PermissionsManage)]
    public async Task<ActionResult<RoleDto>> SetRolePermissions(Guid roleId, [FromBody] SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var updated = await _rbacService.SetRolePermissionsAsync(request.TenantId, roleId, request.PermissionIds, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("users/{userId:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<IActionResult> AssignRole(Guid userId, Guid roleId, [FromQuery] TenantScopedQuery request, CancellationToken cancellationToken)
    {
        var assigned = await _rbacService.AssignRoleToUserAsync(request.TenantId, userId, roleId, cancellationToken);
        return assigned ? NoContent() : NotFound();
    }

    [HttpDelete("users/{userId:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<IActionResult> RemoveRole(Guid userId, Guid roleId, [FromQuery] TenantScopedQuery request, CancellationToken cancellationToken)
    {
        var removed = await _rbacService.RemoveRoleFromUserAsync(request.TenantId, userId, roleId, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    [HttpGet("users")]
    [Authorize(Policy = PermissionCodes.UsersManage)]
    public async Task<ActionResult<IReadOnlyList<RbacUserDto>>> ListUsers([FromQuery] TenantScopedQuery request, CancellationToken cancellationToken)
    {
        var result = await _rbacService.ListUsersAsync(request.TenantId, cancellationToken);
        return Ok(result);
    }
}

public sealed record UpdatePermissionApiRequest(Guid TenantId, string Name, string Code, string? Description);
public sealed record UpdateRoleApiRequest(Guid TenantId, string Name, string Code, string? Description);
public sealed record SetRolePermissionsRequest(Guid TenantId, IReadOnlyCollection<Guid> PermissionIds);
public sealed record TenantScopedQuery(Guid TenantId);

