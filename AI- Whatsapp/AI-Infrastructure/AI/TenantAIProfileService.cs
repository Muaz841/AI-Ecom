using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.AI;

public sealed class TenantAIProfileService : ITenantAIProfileService
{
    private readonly ITenantAIProfileRepository _repository;
    private readonly ICacheService _cache;

    public TenantAIProfileService(ITenantAIProfileRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<TenantAIProfileResult?> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
    {
        var profile = await _repository.GetByTenantIdAsync(tenantId, ct);
        return profile is null ? null : MapToResult(profile);
    }

    public async Task<TenantAIProfileResult> SaveProfileAsync(
        Guid tenantId,
        SaveTenantAIProfileRequest request,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetByTenantIdAsync(tenantId, ct);

        TenantAIProfile profile;
        if (existing is null)
        {
            profile = TenantAIProfile.Create(
                tenantId,
                request.SystemPrompt,
                request.Tone,
                request.Language,
                request.BrandRules,
                request.ForbiddenTopics,
                request.DefaultResponseStyle,
                request.AiCallsPerHourLimit);
        }
        else
        {
            existing.Update(
                request.SystemPrompt,
                request.Tone,
                request.Language,
                request.BrandRules,
                request.ForbiddenTopics,
                request.DefaultResponseStyle,
                request.AiCallsPerHourLimit);
            profile = existing;
        }

        await _repository.SaveAsync(profile, ct);

        // Invalidate cached system prompt so the next AI call picks up the new profile.
        await _cache.RemoveAsync(CacheKeys.AiSettingsTenant(tenantId), ct);

        return MapToResult(profile);
    }

    private static TenantAIProfileResult MapToResult(TenantAIProfile p) => new(
        p.Id,
        p.TenantId,
        p.SystemPrompt,
        p.Tone,
        p.Language,
        p.BrandRules,
        p.ForbiddenTopics,
        p.DefaultResponseStyle,
        p.AiCallsPerHourLimit,
        p.Version,
        p.CreatedAt.ToString("o"),
        p.UpdatedAt?.ToString("o"));
}
