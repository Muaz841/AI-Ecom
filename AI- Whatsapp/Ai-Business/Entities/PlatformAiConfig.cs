using System;

namespace EcomAI.Platform.Business.Entities;

public class PlatformAiConfig : Entity<Guid>
{
    public AIProvider ActiveProvider { get; private set; }

    public bool DebugModeEnabled { get; private set; }

    public string OllamaEndpoint { get; private set; }
    public string OllamaModel    { get; private set; }

    public string  OpenAIModel          { get; private set; }
    public string? OpenAIApiKeyProtected { get; private set; }

    public string  GeminiModel          { get; private set; }
    public string? GeminiApiKeyProtected { get; private set; }

    public int RequestTimeoutSeconds { get; private set; } = 60;

    public bool    EnableToolCalling      { get; private set; }
    public bool    EnableStructuredOutput { get; private set; }
    public double? Temperature            { get; private set; }
    public double? TopP                   { get; private set; }
    public int?    MaxTokens              { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private PlatformAiConfig() { }

    public static PlatformAiConfig Create(
        AIProvider activeProvider,
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
            Id                   = Guid.NewGuid(),
            ActiveProvider       = activeProvider,
            DebugModeEnabled     = debugModeEnabled,
            OllamaEndpoint       = string.IsNullOrWhiteSpace(ollamaEndpoint) ? "http://localhost:11434" : ollamaEndpoint.Trim(),
            OllamaModel          = string.IsNullOrWhiteSpace(ollamaModel) ? "llama3.1:8b" : ollamaModel.Trim(),
            OpenAIModel          = openAIModel?.Trim() ?? string.Empty,
            OpenAIApiKeyProtected = openAIApiKeyProtected,
            GeminiModel          = geminiModel?.Trim() ?? string.Empty,
            GeminiApiKeyProtected = geminiApiKeyProtected,
            RequestTimeoutSeconds = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60,
            EnableToolCalling     = enableToolCalling,
            EnableStructuredOutput = enableStructuredOutput,
            Temperature          = temperature,
            TopP                 = topP,
            MaxTokens            = maxTokens,
            UpdatedAt            = DateTime.UtcNow,
        };
    }

    public void Update(
        AIProvider activeProvider,
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
        ActiveProvider    = activeProvider;
        DebugModeEnabled  = debugModeEnabled;
        OllamaEndpoint    = string.IsNullOrWhiteSpace(ollamaEndpoint) ? "http://localhost:11434" : ollamaEndpoint.Trim();
        OllamaModel       = string.IsNullOrWhiteSpace(ollamaModel) ? "llama3.1:8b" : ollamaModel.Trim();
        OpenAIModel       = openAIModel?.Trim() ?? OpenAIModel;

        if (openAIApiKeyProtected is not null)
            OpenAIApiKeyProtected = openAIApiKeyProtected;

        GeminiModel = geminiModel?.Trim() ?? GeminiModel;

        if (geminiApiKeyProtected is not null)
            GeminiApiKeyProtected = geminiApiKeyProtected;

        RequestTimeoutSeconds  = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60;
        EnableToolCalling      = enableToolCalling;
        EnableStructuredOutput = enableStructuredOutput;
        Temperature            = temperature;
        TopP                   = topP;
        MaxTokens              = maxTokens;
        UpdatedAt              = DateTime.UtcNow;
    }
}
