using System;

namespace EcomAI.Platform.Business.Entities;

public class PlatformMetaConfig : Entity<Guid>
{
    public string AppId { get; private set; } = string.Empty;
    
    public string AppSecretProtected { get; private set; } = string.Empty;

    public string? LoginConfigurationId { get; private set; }

    public string GraphVersion { get; private set; } = "25.0";
    public string CallbackBaseUrl { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }

    private PlatformMetaConfig() { }

    public static PlatformMetaConfig Create(
        string appId,
        string appSecretProtected,
        string? loginConfigurationId,
        string graphVersion,
        string callbackBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("AppId is required.", nameof(appId));

        if (string.IsNullOrWhiteSpace(appSecretProtected))
            throw new ArgumentException("AppSecret (protected) is required.", nameof(appSecretProtected));

        return new PlatformMetaConfig
        {
            Id = Guid.NewGuid(),
            AppId = appId.Trim(),
            AppSecretProtected = appSecretProtected,
            LoginConfigurationId = string.IsNullOrWhiteSpace(loginConfigurationId) ? null : loginConfigurationId.Trim(),
            GraphVersion = string.IsNullOrWhiteSpace(graphVersion) ? "25.0" : graphVersion.Trim().TrimStart('v'),
            CallbackBaseUrl = callbackBaseUrl.Trim(),
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void Update(
        string appId,
        string? appSecretProtected,
        string? loginConfigurationId,
        string graphVersion,
        string callbackBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("AppId is required.", nameof(appId));

        AppId = appId.Trim();

        if (!string.IsNullOrWhiteSpace(appSecretProtected))
        {
            AppSecretProtected = appSecretProtected;
        }

        LoginConfigurationId = string.IsNullOrWhiteSpace(loginConfigurationId) ? null : loginConfigurationId.Trim();
        GraphVersion = string.IsNullOrWhiteSpace(graphVersion) ? "25.0" : graphVersion.Trim().TrimStart('v');
        CallbackBaseUrl = callbackBaseUrl.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
