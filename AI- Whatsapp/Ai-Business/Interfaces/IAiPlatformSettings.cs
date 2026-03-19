using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

// ── Runtime config (decrypted, consumed by AI services) ───────────────────────

/// <summary>
/// Decrypted runtime config read from the DB. Single source of truth — never falls back to appsettings.
/// </summary>
public sealed record AiRuntimeConfig(
    AIProvider ActiveProvider,
    bool DebugModeEnabled,
    string OllamaEndpoint,
    string OllamaModel,
    string? OpenAIModel,           // null = not yet selected by host
    string? OpenAIApiKey,          // null when not configured
    string? GeminiModel,           // null = not yet selected by host
    string? GeminiApiKey,          // null when not configured
    int RequestTimeoutSeconds,
    bool EnableToolCalling,
    bool EnableStructuredOutput,
    double? Temperature,
    double? TopP,
    int? MaxTokens);

/// <summary>
/// Resolves the current AI provider config from the database at runtime.
/// Returns null when no DB record exists — callers must handle this as a
/// "not configured" state rather than falling back to appsettings.
/// </summary>
public interface IAiRuntimeConfigProvider
{
    Task<AiRuntimeConfig?> GetRuntimeConfigAsync(CancellationToken cancellationToken = default);
}

// ── Repository ────────────────────────────────────────────────────────────────

public interface IPlatformAiConfigRepository
{
    Task<EcomAI.Platform.Business.Entities.PlatformAiConfig?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(EcomAI.Platform.Business.Entities.PlatformAiConfig config, CancellationToken cancellationToken = default);
}

// ── Model catalog DTOs ────────────────────────────────────────────────────────

public sealed record AiModelInfoDto(
    string Name,
    string Label,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    int ContextWindow,
    bool IsPreview);

public sealed record AiModelListResult(
    string Provider,
    IReadOnlyList<AiModelInfoDto> Models,
    bool IsCached);

// ── UI DTOs ────────────────────────────────────────────────────────────────────

/// <summary>Response DTO — API keys always masked. ActiveProvider is string for JSON serialization compatibility.</summary>
public sealed record PlatformAiConfigResult(
    bool IsConfigured,
    string ActiveProvider,          // "OpenAI" | "Gemini" | "Ollama"
    bool DebugModeEnabled,
    string OllamaEndpoint,
    string OllamaModel,
    string? OpenAIModel,
    bool OpenAIApiKeySet,
    string? OpenAIApiKeyMasked,
    string? GeminiModel,
    bool GeminiApiKeySet,
    string? GeminiApiKeyMasked,
    int RequestTimeoutSeconds,
    bool EnableToolCalling,
    bool EnableStructuredOutput,
    double? Temperature,
    double? TopP,
    int? MaxTokens,
    string? UpdatedAt);

/// <summary>Save request — ActiveProvider arrives as string from Angular, parsed to AIProvider enum in the service.</summary>
public sealed record SavePlatformAiConfigRequest(
    string ActiveProvider,
    bool DebugModeEnabled,
    string? OllamaEndpoint,
    string? OllamaModel,
    string? OpenAIModel,
    string? OpenAIApiKey,
    string? GeminiModel,
    string? GeminiApiKey,
    int RequestTimeoutSeconds,
    bool EnableToolCalling = false,
    bool EnableStructuredOutput = false,
    double? Temperature = null,
    double? TopP = null,
    int? MaxTokens = null);

// ── Service ────────────────────────────────────────────────────────────────────

public interface IPlatformAiSettingsService
{
    Task<PlatformAiConfigResult> GetAiConfigAsync(CancellationToken cancellationToken = default);
    Task<PlatformAiConfigResult> SaveAiConfigAsync(SavePlatformAiConfigRequest request, CancellationToken cancellationToken = default);
    Task<AiModelListResult> GetModelsAsync(string provider, bool forceRefresh = false, CancellationToken cancellationToken = default);
}
