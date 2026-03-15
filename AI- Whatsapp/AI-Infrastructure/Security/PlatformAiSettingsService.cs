using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class PlatformAiSettingsService : IPlatformAiSettingsService, IAiRuntimeConfigProvider
{
    private const string KeyMask = "••••••••••••••••";

    // ── Curated model catalog (OpenAI) ────────────────────────────────────────
    private static readonly IReadOnlyList<AiModelInfoDto> OpenAiModels =
    [
        new("gpt-4o",          "GPT-4o",           true,  true,  128_000, false),
        new("gpt-4o-mini",     "GPT-4o Mini",      true,  true,  128_000, false),
        new("gpt-4-turbo",     "GPT-4 Turbo",      true,  false, 128_000, false),
        new("gpt-3.5-turbo",   "GPT-3.5 Turbo",    true,  false, 16_385,  false),
        new("o1",              "o1",               false, false, 200_000, false),
        new("o1-mini",         "o1-mini",          false, false, 128_000, false),
        new("o3-mini",         "o3-mini",          false, false, 200_000, true),
    ];

    // ── Curated model catalog (Gemini) ────────────────────────────────────────
    private static readonly IReadOnlyList<AiModelInfoDto> GeminiModels =
    [
        new("gemini-2.0-flash",               "Gemini 2.0 Flash",          true,  true,  1_000_000, false),
        new("gemini-2.0-flash-lite",          "Gemini 2.0 Flash Lite",     true,  true,  1_000_000, false),
        new("gemini-1.5-pro",                 "Gemini 1.5 Pro",            true,  true,  2_000_000, false),
        new("gemini-1.5-flash",               "Gemini 1.5 Flash",          true,  true,  1_000_000, false),
        new("gemini-1.5-flash-8b",            "Gemini 1.5 Flash-8B",       true,  true,  1_000_000, false),
        new("gemini-2.0-flash-thinking-exp",  "Gemini 2.0 Thinking (exp)", false, false, 32_000,    true),
    ];

    // ── Known capability map for validation ───────────────────────────────────
    private static readonly Dictionary<string, (bool ToolCalling, bool StructuredOutput)> KnownCapabilities
        = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o"]                            = (true,  true),
        ["gpt-4o-mini"]                       = (true,  true),
        ["gpt-4-turbo"]                       = (true,  false),
        ["gpt-3.5-turbo"]                     = (true,  false),
        ["o1"]                                = (false, false),
        ["o1-mini"]                           = (false, false),
        ["o3-mini"]                           = (false, false),
        ["gemini-2.0-flash"]                  = (true,  true),
        ["gemini-2.0-flash-lite"]             = (true,  true),
        ["gemini-1.5-pro"]                    = (true,  true),
        ["gemini-1.5-flash"]                  = (true,  true),
        ["gemini-1.5-flash-8b"]               = (true,  true),
        ["gemini-2.0-flash-thinking-exp"]     = (false, false),
    };

    private readonly IPlatformAiConfigRepository _repository;
    private readonly ITokenProtector _tokenProtector;
    private readonly ICacheService _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PlatformAiSettingsService> _logger;

    public PlatformAiSettingsService(
        IPlatformAiConfigRepository repository,
        ITokenProtector tokenProtector,
        ICacheService cache,
        IHttpClientFactory httpClientFactory,
        ILogger<PlatformAiSettingsService> logger)
    {
        _repository = repository;
        _tokenProtector = tokenProtector;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── IPlatformAiSettingsService ────────────────────────────────────────────

    public async Task<PlatformAiConfigResult> GetAiConfigAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<PlatformAiConfigResult>(CacheKeys.AiSettingsHost, cancellationToken);
        if (cached is not null) return cached;

        var config = await _repository.GetAsync(cancellationToken);
        var result = config is null ? DefaultResult() : MapToResult(config);

        await _cache.SetAsync(CacheKeys.AiSettingsHost, result, CacheKeys.SettingsTtl, cancellationToken);
        return result;
    }

    public async Task<PlatformAiConfigResult> SaveAiConfigAsync(
        SavePlatformAiConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateCapabilitiesAsync(request, cancellationToken);

        var existing = await _repository.GetAsync(cancellationToken);

        var encryptedOpenAI = ShouldRotateKey(request.OpenAIApiKey)
            ? _tokenProtector.Protect(request.OpenAIApiKey!)
            : null;

        var encryptedGemini = ShouldRotateKey(request.GeminiApiKey)
            ? _tokenProtector.Protect(request.GeminiApiKey!)
            : null;

        if (existing is null)
        {
            var created = PlatformAiConfig.Create(
                activeProvider: request.ActiveProvider,
                debugModeEnabled: request.DebugModeEnabled,
                ollamaEndpoint: request.OllamaEndpoint,
                ollamaModel: request.OllamaModel,
                openAIModel: request.OpenAIModel,
                openAIApiKeyProtected: encryptedOpenAI,
                geminiModel: request.GeminiModel,
                geminiApiKeyProtected: encryptedGemini,
                requestTimeoutSeconds: request.RequestTimeoutSeconds,
                enableToolCalling: request.EnableToolCalling,
                enableStructuredOutput: request.EnableStructuredOutput,
                temperature: request.Temperature,
                topP: request.TopP,
                maxTokens: request.MaxTokens);

            await _repository.SaveAsync(created, cancellationToken);
        }
        else
        {
            existing.Update(
                activeProvider: request.ActiveProvider,
                debugModeEnabled: request.DebugModeEnabled,
                ollamaEndpoint: request.OllamaEndpoint,
                ollamaModel: request.OllamaModel,
                openAIModel: request.OpenAIModel,
                openAIApiKeyProtected: encryptedOpenAI,
                geminiModel: request.GeminiModel,
                geminiApiKeyProtected: encryptedGemini,
                requestTimeoutSeconds: request.RequestTimeoutSeconds,
                enableToolCalling: request.EnableToolCalling,
                enableStructuredOutput: request.EnableStructuredOutput,
                temperature: request.Temperature,
                topP: request.TopP,
                maxTokens: request.MaxTokens);

            await _repository.SaveAsync(existing, cancellationToken);
        }

        await _cache.RemoveAsync(CacheKeys.AiSettingsHost, cancellationToken);

        var saved = await _repository.GetAsync(cancellationToken);
        var result = MapToResult(saved!);
        await _cache.SetAsync(CacheKeys.AiSettingsHost, result, CacheKeys.SettingsTtl, cancellationToken);
        return result;
    }

    public async Task<AiModelListResult> GetModelsAsync(
        string provider,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AiModels(provider);

        if (!forceRefresh)
        {
            var cached = await _cache.GetAsync<List<AiModelInfoDto>>(cacheKey, cancellationToken);
            if (cached is not null)
                return new AiModelListResult(provider, cached, IsCached: true);
        }

        var models = provider.Trim().ToLowerInvariant() switch
        {
            "openai" => new List<AiModelInfoDto>(OpenAiModels),
            "gemini" => await FetchGeminiModelsWithFallbackAsync(cancellationToken),
            "ollama" => await FetchOllamaModelsAsync(cancellationToken),
            "mock"   => [new AiModelInfoDto("mock-model", "Mock (Debug)", false, false, 0, false)],
            _        => new List<AiModelInfoDto>(),
        };

        await _cache.SetAsync(cacheKey, models, CacheKeys.ModelsTtl, cancellationToken);
        return new AiModelListResult(provider, models, IsCached: false);
    }

    // ── IAiRuntimeConfigProvider ──────────────────────────────────────────────

    public async Task<AiRuntimeConfig?> GetRuntimeConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetAsync(cancellationToken);
        if (config is null) return null;

        string? openAIApiKey = null;
        if (!string.IsNullOrWhiteSpace(config.OpenAIApiKeyProtected))
        {
            try { openAIApiKey = _tokenProtector.Unprotect(config.OpenAIApiKeyProtected); }
            catch { /* key corrupted — treat as missing */ }
        }

        string? geminiApiKey = null;
        if (!string.IsNullOrWhiteSpace(config.GeminiApiKeyProtected))
        {
            try { geminiApiKey = _tokenProtector.Unprotect(config.GeminiApiKeyProtected); }
            catch { /* key corrupted — treat as missing */ }
        }

        return new AiRuntimeConfig(
            ActiveProvider: config.ActiveProvider,
            DebugModeEnabled: config.DebugModeEnabled,
            OllamaEndpoint: config.OllamaEndpoint,
            OllamaModel: config.OllamaModel,
            OpenAIModel: config.OpenAIModel,
            OpenAIApiKey: openAIApiKey,
            GeminiModel: config.GeminiModel,
            GeminiApiKey: geminiApiKey,
            RequestTimeoutSeconds: config.RequestTimeoutSeconds,
            EnableToolCalling: config.EnableToolCalling,
            EnableStructuredOutput: config.EnableStructuredOutput,
            Temperature: config.Temperature,
            TopP: config.TopP,
            MaxTokens: config.MaxTokens);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches Gemini models from the live API when a key is configured;
    /// falls back to the curated catalog when the key is missing or the call fails.
    /// </summary>
    private async Task<List<AiModelInfoDto>> FetchGeminiModelsWithFallbackAsync(CancellationToken ct)
    {
        var config = await _repository.GetAsync(ct);
        if (config is null || string.IsNullOrWhiteSpace(config.GeminiApiKeyProtected))
        {
            _logger.LogInformation("No Gemini API key configured. Using curated model catalog.");
            return new List<AiModelInfoDto>(GeminiModels);
        }

        string? apiKey = null;
        try { apiKey = _tokenProtector.Unprotect(config.GeminiApiKeyProtected); }
        catch { /* key corrupted */ }

        if (string.IsNullOrWhiteSpace(apiKey))
            return new List<AiModelInfoDto>(GeminiModels);

        return await FetchGeminiModelsAsync(apiKey, ct);
    }

    private async Task<List<AiModelInfoDto>> FetchGeminiModelsAsync(string apiKey, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}&pageSize=100";
            var response = await client.GetFromJsonAsync<GeminiModelsResponse>(url, ct);
            if (response?.Models is null) return new List<AiModelInfoDto>(GeminiModels);

            var result = response.Models
                .Where(m => m.SupportedGenerationMethods.Contains("generateContent"))
                .Select(MapGeminiModel)
                .OrderBy(m => m.Name)
                .ToList();

            return result.Count > 0 ? result : new List<AiModelInfoDto>(GeminiModels);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Gemini model list from live API. Using curated catalog as fallback.");
            return new List<AiModelInfoDto>(GeminiModels);
        }
    }

    private static AiModelInfoDto MapGeminiModel(GeminiModelEntry m)
    {
        var name = m.Name.StartsWith("models/", StringComparison.Ordinal)
            ? m.Name["models/".Length..]
            : m.Name;

        var label = !string.IsNullOrWhiteSpace(m.DisplayName) ? m.DisplayName : name;

        // Thinking / LearnLM models do not support function calling or structured output
        var nameLower = name.ToLowerInvariant();
        var isLimited = nameLower.Contains("thinking") || nameLower.Contains("learnlm");

        var isPreview = nameLower.Contains("preview") || nameLower.Contains("-exp")
                     || nameLower.Contains("-latest") || nameLower.Contains("experimental");

        return new AiModelInfoDto(
            Name: name,
            Label: label,
            SupportsToolCalling: !isLimited,
            SupportsStructuredOutput: !isLimited,
            ContextWindow: m.InputTokenLimit,
            IsPreview: isPreview);
    }

    private async Task<List<AiModelInfoDto>> FetchOllamaModelsAsync(CancellationToken ct)
    {
        try
        {
            var config = await _repository.GetAsync(ct);
            var endpoint = config?.OllamaEndpoint ?? "http://localhost:11434";

            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetFromJsonAsync<OllamaTagsResponse>("api/tags", ct);
            if (response?.Models is null) return [];

            return response.Models.ConvertAll(m => new AiModelInfoDto(
                Name: m.Name,
                Label: m.Name,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                ContextWindow: 0,
                IsPreview: false));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Ollama model list from local endpoint.");
            return [];
        }
    }

    /// <summary>
    /// Validates capability constraints against the live-cached model list first,
    /// then falls back to the static KnownCapabilities dictionary.
    /// Unknown models pass validation (allow save).
    /// </summary>
    private async Task ValidateCapabilitiesAsync(SavePlatformAiConfigRequest request, CancellationToken ct)
    {
        var model = request.ActiveProvider.Trim().ToLowerInvariant() switch
        {
            "openai" => request.OpenAIModel,
            "gemini" => request.GeminiModel,
            _        => null, // Ollama / Mock — skip capability validation
        };

        if (model is null) return;

        // 1. Try live-cached model list (populated by GetModelsAsync)
        var cacheKey = CacheKeys.AiModels(request.ActiveProvider);
        var cachedModels = await _cache.GetAsync<List<AiModelInfoDto>>(cacheKey, ct);
        if (cachedModels is not null)
        {
            var info = cachedModels.FirstOrDefault(m =>
                string.Equals(m.Name, model, StringComparison.OrdinalIgnoreCase));
            if (info is not null)
            {
                EnforceCapabilities(model, info.SupportsToolCalling, info.SupportsStructuredOutput, request);
                return;
            }
        }

        // 2. Fall back to static dictionary
        if (KnownCapabilities.TryGetValue(model, out var caps))
            EnforceCapabilities(model, caps.ToolCalling, caps.StructuredOutput, request);

        // 3. Unknown model → allow save without blocking
    }

    private static void EnforceCapabilities(
        string model,
        bool toolCalling,
        bool structuredOutput,
        SavePlatformAiConfigRequest request)
    {
        if (request.EnableToolCalling && !toolCalling)
            throw new ArgumentException(
                $"Model '{model}' does not support tool calling. Disable 'Enable Tool Calling' or choose a compatible model.");

        if (request.EnableStructuredOutput && !structuredOutput)
            throw new ArgumentException(
                $"Model '{model}' does not support structured output. Disable 'Enable Structured Output' or choose a compatible model.");
    }

    private static PlatformAiConfigResult MapToResult(PlatformAiConfig config)
    {
        var openAIKeySet = !string.IsNullOrWhiteSpace(config.OpenAIApiKeyProtected);
        var geminiKeySet = !string.IsNullOrWhiteSpace(config.GeminiApiKeyProtected);

        return new PlatformAiConfigResult(
            IsConfigured: true,
            ActiveProvider: config.ActiveProvider,
            DebugModeEnabled: config.DebugModeEnabled,
            OllamaEndpoint: config.OllamaEndpoint,
            OllamaModel: config.OllamaModel,
            OpenAIModel: config.OpenAIModel,
            OpenAIApiKeySet: openAIKeySet,
            OpenAIApiKeyMasked: openAIKeySet ? KeyMask : null,
            GeminiModel: config.GeminiModel,
            GeminiApiKeySet: geminiKeySet,
            GeminiApiKeyMasked: geminiKeySet ? KeyMask : null,
            RequestTimeoutSeconds: config.RequestTimeoutSeconds,
            EnableToolCalling: config.EnableToolCalling,
            EnableStructuredOutput: config.EnableStructuredOutput,
            Temperature: config.Temperature,
            TopP: config.TopP,
            MaxTokens: config.MaxTokens,
            UpdatedAt: config.UpdatedAt.ToString("o"));
    }

    private static PlatformAiConfigResult DefaultResult() => new(
        IsConfigured: false,
        ActiveProvider: "Ollama",
        DebugModeEnabled: true,
        OllamaEndpoint: "http://localhost:11434",
        OllamaModel: "llama3.1:8b",
        OpenAIModel: "gpt-4o-mini",
        OpenAIApiKeySet: false,
        OpenAIApiKeyMasked: null,
        GeminiModel: "gemini-1.5-flash",
        GeminiApiKeySet: false,
        GeminiApiKeyMasked: null,
        RequestTimeoutSeconds: 60,
        EnableToolCalling: false,
        EnableStructuredOutput: false,
        Temperature: null,
        TopP: null,
        MaxTokens: null,
        UpdatedAt: null);

    private static bool ShouldRotateKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Replace("•", string.Empty).Trim().Length > 0;
    }

    // ── Gemini API response types ─────────────────────────────────────────────
    private sealed class GeminiModelsResponse
    {
        [JsonPropertyName("models")]
        public List<GeminiModelEntry> Models { get; set; } = [];
    }

    private sealed class GeminiModelEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("supportedGenerationMethods")]
        public List<string> SupportedGenerationMethods { get; set; } = [];

        [JsonPropertyName("inputTokenLimit")]
        public int InputTokenLimit { get; set; }
    }

    // ── Ollama API response types ─────────────────────────────────────────────
    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelEntry> Models { get; set; } = [];
    }

    private sealed class OllamaModelEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
