using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

/// <summary>Decrypted runtime config used internally by infrastructure services (never exposed to API clients).</summary>
public sealed record MetaOAuthRuntimeConfig(
    string AppId,
    string AppSecret,
    string GraphVersion,
    string CallbackBaseUrl,
    string? LoginConfigurationId);

/// <summary>
/// Provides effective Meta OAuth runtime credentials.
/// Implementations resolve from DB first, falling back to appsettings.
/// </summary>
public interface IMetaOAuthRuntimeConfigProvider
{
    Task<MetaOAuthRuntimeConfig?> GetRuntimeConfigAsync(CancellationToken cancellationToken = default);
}
