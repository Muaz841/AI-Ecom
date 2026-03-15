using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

// ── Result DTO ────────────────────────────────────────────────────────────────

public sealed record TenantAIProfileResult(
    Guid Id,
    Guid TenantId,
    string SystemPrompt,
    string? Tone,
    string? Language,
    string? BrandRules,
    string? ForbiddenTopics,
    string? DefaultResponseStyle,
    int AiCallsPerHourLimit,
    int Version,
    string CreatedAt,
    string? UpdatedAt);

// ── Save request ──────────────────────────────────────────────────────────────

public sealed record SaveTenantAIProfileRequest(
    string SystemPrompt,
    string? Tone = null,
    string? Language = null,
    string? BrandRules = null,
    string? ForbiddenTopics = null,
    string? DefaultResponseStyle = null,
    int AiCallsPerHourLimit = 200);

// ── Repository ────────────────────────────────────────────────────────────────

public interface ITenantAIProfileRepository
{
    Task<TenantAIProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveAsync(TenantAIProfile profile, CancellationToken ct = default);
}

// ── Service ───────────────────────────────────────────────────────────────────

public interface ITenantAIProfileService
{
    Task<TenantAIProfileResult?> GetProfileAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantAIProfileResult> SaveProfileAsync(Guid tenantId, SaveTenantAIProfileRequest request, CancellationToken ct = default);
}
