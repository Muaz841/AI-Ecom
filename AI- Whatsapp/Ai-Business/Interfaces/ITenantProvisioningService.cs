using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface ITenantProvisioningService
{
    Task<TenantProvisionResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<TenantDetailDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record CreateTenantRequest(
    string Name,
    string BusinessName,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName);

public sealed record TenantProvisionResult(
    bool Success,
    Guid? TenantId = null,
    string? AdminUserId = null,
    string? ErrorMessage = null);

public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string BusinessName,
    bool IsActive,
    DateTime CreatedAt,
    int UserCount);

public sealed record TenantDetailDto(
    Guid Id,
    string Name,
    string BusinessName,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<TenantUserDto> Users);

public sealed record TenantUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
