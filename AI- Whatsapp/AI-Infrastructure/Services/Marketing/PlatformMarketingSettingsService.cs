using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

public sealed class PlatformMarketingSettingsService : IPlatformMarketingSettingsService
{
    private const string ApiKeyMask   = "sk-ant-•••••••••••••••••";
    private const string TokenMask    = "EAAx•••••••••••••••••••";
    private const string MaskSentinel = "•";

    private readonly IPlatformMarketingConfigRepository _repo;
    private readonly ITokenProtector _protector;

    public PlatformMarketingSettingsService(
        IPlatformMarketingConfigRepository repo,
        ITokenProtector protector)
    {
        _repo      = repo;
        _protector = protector;
    }

    public async Task<PlatformMarketingConfigResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await _repo.GetAsync(cancellationToken);
        return config is null ? DefaultResult() : MapToResult(config);
    }

    public async Task<PlatformMarketingConfigResult> SaveAsync(
        SavePlatformMarketingConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetAsync(cancellationToken);

        // Only encrypt and overwrite when a real (non-masked) value is submitted.
        string? encryptedApiKey = IsMasked(request.ClaudeApiKey)
            ? null
            : string.IsNullOrWhiteSpace(request.ClaudeApiKey)
                ? null
                : _protector.Protect(request.ClaudeApiKey);

        string? encryptedAdsToken = IsMasked(request.MetaAdsAccessToken)
            ? null
            : string.IsNullOrWhiteSpace(request.MetaAdsAccessToken)
                ? null
                : _protector.Protect(request.MetaAdsAccessToken);

        if (existing is null)
        {
            var config = PlatformMarketingConfig.Create(
                claudeApiKeyProtected:       encryptedApiKey,
                claudeDecisionModel:         request.ClaudeDecisionModel,
                claudeSummaryModel:          request.ClaudeSummaryModel,
                metaAdsAccountId:            request.MetaAdsAccountId,
                metaAdsAccessTokenProtected: encryptedAdsToken,
                dryRun:                      request.DryRun,
                maxActionsPerDay:            request.MaxActionsPerDay,
                dailySpendCapUsd:            request.DailySpendCapUsd);

            await _repo.SaveAsync(config, cancellationToken);
            return MapToResult(config);
        }

        existing.Update(
            claudeApiKeyProtected:       encryptedApiKey,
            claudeDecisionModel:         request.ClaudeDecisionModel,
            claudeSummaryModel:          request.ClaudeSummaryModel,
            metaAdsAccountId:            request.MetaAdsAccountId,
            metaAdsAccessTokenProtected: encryptedAdsToken,
            dryRun:                      request.DryRun,
            maxActionsPerDay:            request.MaxActionsPerDay,
            dailySpendCapUsd:            request.DailySpendCapUsd);

        await _repo.SaveAsync(existing, cancellationToken);
        return MapToResult(existing);
    }

    private static bool IsMasked(string? value) =>
        !string.IsNullOrEmpty(value) && value.Contains(MaskSentinel);

    private static PlatformMarketingConfigResult MapToResult(PlatformMarketingConfig config) =>
        new(
            IsConfigured:             config.IsConfigured,
            ClaudeApiKeySet:          config.ClaudeApiKeyProtected != null,
            ClaudeApiKeyMasked:       config.ClaudeApiKeyProtected != null ? ApiKeyMask : null,
            ClaudeDecisionModel:      config.ClaudeDecisionModel,
            ClaudeSummaryModel:       config.ClaudeSummaryModel,
            MetaAdsAccountId:         config.MetaAdsAccountId,
            MetaAdsAccessTokenSet:    config.MetaAdsAccessTokenProtected != null,
            MetaAdsAccessTokenMasked: config.MetaAdsAccessTokenProtected != null ? TokenMask : null,
            DryRun:                   config.DryRun,
            MaxActionsPerDay:         config.MaxActionsPerDay,
            DailySpendCapUsd:         config.DailySpendCapUsd,
            UpdatedAt:                config.UpdatedAt.ToString("o"));

    private static PlatformMarketingConfigResult DefaultResult() =>
        new(
            IsConfigured:             false,
            ClaudeApiKeySet:          false,
            ClaudeApiKeyMasked:       null,
            ClaudeDecisionModel:      "claude-opus-4-6",
            ClaudeSummaryModel:       "claude-haiku-4-5-20251001",
            MetaAdsAccountId:         null,
            MetaAdsAccessTokenSet:    false,
            MetaAdsAccessTokenMasked: null,
            DryRun:                   true,
            MaxActionsPerDay:         10,
            DailySpendCapUsd:         100m,
            UpdatedAt:                null);
}
