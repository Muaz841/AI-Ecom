using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

// ── Runtime config (decrypted, consumed by AI services) ───────────────────────

/// <summary>
/// Decrypted runtime config read from the DB. Returned by IAiRuntimeConfigProvider.
/// All string fields are guaranteed non-null when returned (defaulted if blank in DB).
/// </summary>
public sealed record AiRuntimeConfig(
    string ActiveProvider,        // "OpenAI" | "Gemini" | "Ollama" | "Mock"
    bool DebugModeEnabled,
    string OllamaEndpoint,
    string OllamaModel,
    string OpenAIModel,
    string? OpenAIApiKey,          // null when not configured
    string GeminiModel,
    string? GeminiApiKey,          // null when not configured
    int RequestTimeoutSeconds,
    bool EnableToolCalling,
    bool EnableStructuredOutput,
    double? Temperature,
    double? TopP,
    int? MaxTokens);

/// <summary>
/// Resolves the current AI provider config from the database at runtime.
/// Returns null when no DB record exists (factory falls back to appsettings).
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

/// <summary>Metadata about a specific AI model from a provider.</summary>
public sealed record AiModelInfoDto(
    string Name,
    string Label,
    bool SupportsToolCalling,
    bool SupportsStructuredOutput,
    int ContextWindow,
    bool IsPreview);

/// <summary>List of available models returned by GetModelsAsync.</summary>
public sealed record AiModelListResult(
    string Provider,
    IReadOnlyList<AiModelInfoDto> Models,
    bool IsCached);

// ── UI DTOs ────────────────────────────────────────────────────────────────────

/// <summary>Response DTO — API keys are always masked, never returned as plaintext.</summary>
public sealed record PlatformAiConfigResult(
    bool IsConfigured,
    string ActiveProvider,
    bool DebugModeEnabled,
    string OllamaEndpoint,
    string OllamaModel,
    string OpenAIModel,
    bool OpenAIApiKeySet,
    string? OpenAIApiKeyMasked,
    string GeminiModel,
    bool GeminiApiKeySet,
    string? GeminiApiKeyMasked,
    int RequestTimeoutSeconds,
    bool EnableToolCalling,
    bool EnableStructuredOutput,
    double? Temperature,
    double? TopP,
    int? MaxTokens,
    string? UpdatedAt);

public sealed record SavePlatformAiConfigRequest(
    string ActiveProvider,
    bool DebugModeEnabled,
    string OllamaEndpoint,
    string OllamaModel,
    string OpenAIModel,
    /// <summary>Null or masked placeholder = keep existing key. New value = rotate key.</summary>
    string? OpenAIApiKey,
    string GeminiModel,
    /// <summary>Null or masked placeholder = keep existing key. New value = rotate key.</summary>
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
