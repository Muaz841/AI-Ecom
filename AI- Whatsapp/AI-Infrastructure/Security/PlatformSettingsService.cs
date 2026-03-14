using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class PlatformSettingsService : IPlatformSettingsService, IMetaOAuthRuntimeConfigProvider
{
    private const string SecretMask = "••••••••••••••••";

    private readonly IPlatformMetaConfigRepository _repository;
    private readonly ITokenProtector _tokenProtector;

    public PlatformSettingsService(
        IPlatformMetaConfigRepository repository,
        ITokenProtector tokenProtector)
    {
        _repository = repository;
        _tokenProtector = tokenProtector;
    }

    public async Task<PlatformMetaConfigResult> GetMetaConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetAsync(cancellationToken);
        if (config is null)
        {
            return new PlatformMetaConfigResult(
                IsConfigured: false,
                AppId: string.Empty,
                AppSecretMasked: string.Empty,
                LoginConfigurationId: null,
                GraphVersion: "25.0",
                CallbackBaseUrl: string.Empty,
                UpdatedAt: null);
        }

        return new PlatformMetaConfigResult(
            IsConfigured: !string.IsNullOrWhiteSpace(config.AppId) && !string.IsNullOrWhiteSpace(config.AppSecretProtected),
            AppId: config.AppId,
            AppSecretMasked: SecretMask,
            LoginConfigurationId: config.LoginConfigurationId,
            GraphVersion: config.GraphVersion,
            CallbackBaseUrl: config.CallbackBaseUrl,
            UpdatedAt: config.UpdatedAt.ToString("o"));
    }

    public async Task<PlatformMetaConfigResult> SaveMetaConfigAsync(
        SavePlatformMetaConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AppId))
            throw new ArgumentException("AppId is required.");

        var existing = await _repository.GetAsync(cancellationToken);

        string? encryptedSecret = null;

        // Only encrypt + update the secret if the caller passed a new non-masked value.
        if (!string.IsNullOrWhiteSpace(request.AppSecret) && !IsMaskedPlaceholder(request.AppSecret))
        {
            encryptedSecret = _tokenProtector.Protect(request.AppSecret);
        }

        if (existing is null)
        {
            if (encryptedSecret is null)
                throw new InvalidOperationException("AppSecret is required when creating the initial configuration.");

            var created = PlatformMetaConfig.Create(
                appId: request.AppId,
                appSecretProtected: encryptedSecret,
                loginConfigurationId: request.LoginConfigurationId,
                graphVersion: request.GraphVersion,
                callbackBaseUrl: request.CallbackBaseUrl ?? string.Empty);

            await _repository.SaveAsync(created, cancellationToken);
            existing = await _repository.GetAsync(cancellationToken);
        }
        else
        {
            existing.Update(
                appId: request.AppId,
                appSecretProtected: encryptedSecret,
                loginConfigurationId: request.LoginConfigurationId,
                graphVersion: request.GraphVersion,
                callbackBaseUrl: request.CallbackBaseUrl ?? string.Empty);

            await _repository.SaveAsync(existing, cancellationToken);
            existing = await _repository.GetAsync(cancellationToken);
        }

        return new PlatformMetaConfigResult(
            IsConfigured: true,
            AppId: existing!.AppId,
            AppSecretMasked: SecretMask,
            LoginConfigurationId: existing.LoginConfigurationId,
            GraphVersion: existing.GraphVersion,
            CallbackBaseUrl: existing.CallbackBaseUrl,
            UpdatedAt: existing.UpdatedAt.ToString("o"));
    }

    // ── IMetaOAuthRuntimeConfigProvider ─────────────────────────────────────

    public async Task<MetaOAuthRuntimeConfig?> GetRuntimeConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetAsync(cancellationToken);

        if (config is not null
            && !string.IsNullOrWhiteSpace(config.AppId)
            && !string.IsNullOrWhiteSpace(config.AppSecretProtected))
        {
            var plainSecret = _tokenProtector.Unprotect(config.AppSecretProtected);
            return new MetaOAuthRuntimeConfig(
                config.AppId,
                plainSecret,
                config.GraphVersion,
                config.CallbackBaseUrl,
                config.LoginConfigurationId);
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────────────

    private static bool IsMaskedPlaceholder(string value)
        => value.Replace("•", string.Empty).Trim().Length == 0;
}
