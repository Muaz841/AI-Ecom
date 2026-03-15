using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

/// <summary>
/// Abstraction over distributed cache (Redis). Serializes/deserializes objects as JSON.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

/// <summary>Well-known cache key prefixes and TTLs.</summary>
public static class CacheKeys
{
    /// <summary>Per-provider model list. TTL: 3 hours.</summary>
    public static string AiModels(string provider) => $"ai:models:{provider.ToLowerInvariant()}";

    /// <summary>Host-level AI settings result. TTL: 10 minutes.</summary>
    public const string AiSettingsHost = "ai:settings:host";

    /// <summary>Per-tenant assembled system prompt. TTL: 10 minutes.</summary>
    public static string AiSettingsTenant(Guid tenantId) => $"ai:settings:tenant:{tenantId}";

    public static readonly TimeSpan ModelsTtl = TimeSpan.FromHours(3);
    public static readonly TimeSpan SettingsTtl = TimeSpan.FromMinutes(10);
}
