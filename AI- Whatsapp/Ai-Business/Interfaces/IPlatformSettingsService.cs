using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public sealed record PlatformMetaConfigResult(
    bool IsConfigured,
    string AppId,
    string AppSecretMasked,
    string? LoginConfigurationId,
    string GraphVersion,
    string CallbackBaseUrl,
    string? UpdatedAt);

public sealed record SavePlatformMetaConfigRequest(
    string AppId,
    /// <summary>Pass null or empty string to keep the existing secret unchanged.</summary>
    string? AppSecret,
    /// <summary>Optional Facebook Login for Business configuration ID.</summary>
    string? LoginConfigurationId,
    string GraphVersion,
    string CallbackBaseUrl);

public interface IPlatformSettingsService
{
    Task<PlatformMetaConfigResult> GetMetaConfigAsync(CancellationToken cancellationToken = default);
    Task<PlatformMetaConfigResult> SaveMetaConfigAsync(SavePlatformMetaConfigRequest request, CancellationToken cancellationToken = default);
}
