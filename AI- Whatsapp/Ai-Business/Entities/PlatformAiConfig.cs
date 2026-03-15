using System;

namespace EcomAI.Platform.Business.Entities;

/// <summary>
/// Singleton host-level AI provider configuration. One row per platform.
/// API keys are stored encrypted via ITokenProtector.
/// </summary>
public class PlatformAiConfig : Entity<Guid>
{
    /// <summary>Active provider name: "OpenAI" | "Gemini" | "Ollama" | "Mock"</summary>
    public string ActiveProvider { get; private set; } = "Ollama";

    public bool DebugModeEnabled { get; private set; } = true;

    // ── Ollama ────────────────────────────────────────────────────────────────
    public string OllamaEndpoint { get; private set; } = "http://localhost:11434";
    public string OllamaModel { get; private set; } = "llama3.1:8b";

    // ── OpenAI ────────────────────────────────────────────────────────────────
    public string OpenAIModel { get; private set; } = "gpt-4o-mini";
    /// <summary>AES-encrypted OpenAI API key. Null when not configured.</summary>
    public string? OpenAIApiKeyProtected { get; private set; }

    // ── Gemini ────────────────────────────────────────────────────────────────
    public string GeminiModel { get; private set; } = "gemini-1.5-flash";
    /// <summary>AES-encrypted Gemini API key. Null when not configured.</summary>
    public string? GeminiApiKeyProtected { get; private set; }

    // ── Shared ────────────────────────────────────────────────────────────────
    public int RequestTimeoutSeconds { get; private set; } = 60;

    // ── Model capabilities ────────────────────────────────────────────────────
    /// <summary>Require the selected model supports function/tool calling.</summary>
    public bool EnableToolCalling { get; private set; }
    /// <summary>Require the selected model supports structured JSON output.</summary>
    public bool EnableStructuredOutput { get; private set; }
    /// <summary>Sampling temperature (0.0–2.0). Null = provider default.</summary>
    public double? Temperature { get; private set; }
    /// <summary>Top-p nucleus sampling (0.0–1.0). Null = provider default.</summary>
    public double? TopP { get; private set; }
    /// <summary>Maximum tokens to generate. Null = provider default.</summary>
    public int? MaxTokens { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private PlatformAiConfig() { }

    public static PlatformAiConfig Create(
        string activeProvider,
        bool debugModeEnabled,
        string? ollamaEndpoint,
        string? ollamaModel,
        string? openAIModel,
        string? openAIApiKeyProtected,
        string? geminiModel,
        string? geminiApiKeyProtected,
        int requestTimeoutSeconds,
        bool enableToolCalling = false,
        bool enableStructuredOutput = false,
        double? temperature = null,
        double? topP = null,
        int? maxTokens = null)
    {
        return new PlatformAiConfig
        {
            Id = Guid.NewGuid(),
            ActiveProvider = NormalizeProvider(activeProvider),
            DebugModeEnabled = debugModeEnabled,
            OllamaEndpoint = string.IsNullOrWhiteSpace(ollamaEndpoint) ? "http://localhost:11434" : ollamaEndpoint.Trim(),
            OllamaModel = string.IsNullOrWhiteSpace(ollamaModel) ? "llama3.1:8b" : ollamaModel.Trim(),
            OpenAIModel = openAIModel?.Trim() ?? string.Empty,
            OpenAIApiKeyProtected = openAIApiKeyProtected,
            GeminiModel = geminiModel?.Trim() ?? string.Empty,
            GeminiApiKeyProtected = geminiApiKeyProtected,
            RequestTimeoutSeconds = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60,
            EnableToolCalling = enableToolCalling,
            EnableStructuredOutput = enableStructuredOutput,
            Temperature = temperature,
            TopP = topP,
            MaxTokens = maxTokens,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void Update(
        string activeProvider,
        bool debugModeEnabled,
        string? ollamaEndpoint,
        string? ollamaModel,
        string? openAIModel,
        string? openAIApiKeyProtected,
        string? geminiModel,
        string? geminiApiKeyProtected,
        int requestTimeoutSeconds,
        bool enableToolCalling = false,
        bool enableStructuredOutput = false,
        double? temperature = null,
        double? topP = null,
        int? maxTokens = null)
    {
        ActiveProvider = NormalizeProvider(activeProvider);
        DebugModeEnabled = debugModeEnabled;
        OllamaEndpoint = string.IsNullOrWhiteSpace(ollamaEndpoint) ? "http://localhost:11434" : ollamaEndpoint.Trim();
        OllamaModel = string.IsNullOrWhiteSpace(ollamaModel) ? "llama3.1:8b" : ollamaModel.Trim();
        OpenAIModel = openAIModel?.Trim() ?? OpenAIModel;

        // Only rotate keys when a new non-null protected value is provided.
        if (openAIApiKeyProtected is not null)
            OpenAIApiKeyProtected = openAIApiKeyProtected;

        GeminiModel = geminiModel?.Trim() ?? GeminiModel;

        if (geminiApiKeyProtected is not null)
            GeminiApiKeyProtected = geminiApiKeyProtected;

        RequestTimeoutSeconds = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60;
        EnableToolCalling = enableToolCalling;
        EnableStructuredOutput = enableStructuredOutput;
        Temperature = temperature;
        TopP = topP;
        MaxTokens = maxTokens;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return "Ollama";
        return provider.Trim();
    }
}
