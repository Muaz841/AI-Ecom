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

    // ── Per-task model overrides ──────────────────────────────────────────────
    // When set, these override the default chat model for specific AI tasks.
    // Allows selecting a dedicated vision model for pose extraction and a
    // dedicated image generation model separately from the chat model.

    /// <summary>Model used for vision tasks (pose extraction). e.g. "gpt-4o", "gemini-1.5-pro".</summary>
    public string? VisionModelName          { get; private set; }

    /// <summary>Model used for image generation. e.g. "dall-e-3", "gemini-2.0-flash-exp".</summary>
    public string? ImageGenerationModelName { get; private set; }

    /// <summary>Model used for messaging/chat generation. Falls back to GeminiModel when null.</summary>
    public string? MessagingModelName { get; private set; }

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
        int? maxTokens = null,
        string? visionModelName = null,
        string? imageGenerationModelName = null,
        string? messagingModelName = null)
    {
        return new PlatformAiConfig
        {
            Id                      = Guid.NewGuid(),
            ActiveProvider          = activeProvider,
            DebugModeEnabled        = debugModeEnabled,
            OllamaEndpoint          = string.IsNullOrWhiteSpace(ollamaEndpoint) ? "http://localhost:11434" : ollamaEndpoint.Trim(),
            OllamaModel             = string.IsNullOrWhiteSpace(ollamaModel) ? "llama3.1:8b" : ollamaModel.Trim(),
            OpenAIModel             = openAIModel?.Trim() ?? string.Empty,
            OpenAIApiKeyProtected   = openAIApiKeyProtected,
            GeminiModel             = geminiModel?.Trim() ?? string.Empty,
            GeminiApiKeyProtected   = geminiApiKeyProtected,
            RequestTimeoutSeconds   = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60,
            EnableToolCalling       = enableToolCalling,
            EnableStructuredOutput  = enableStructuredOutput,
            Temperature             = temperature,
            TopP                    = topP,
            MaxTokens               = maxTokens,
            VisionModelName         = string.IsNullOrWhiteSpace(visionModelName) ? null : visionModelName.Trim(),
            ImageGenerationModelName = string.IsNullOrWhiteSpace(imageGenerationModelName) ? null : imageGenerationModelName.Trim(),
            MessagingModelName       = string.IsNullOrWhiteSpace(messagingModelName) ? null : messagingModelName.Trim(),
            UpdatedAt               = DateTime.UtcNow,
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
        int? maxTokens = null,
        string? visionModelName = null,
        string? imageGenerationModelName = null,
        string? messagingModelName = null)
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

        RequestTimeoutSeconds   = requestTimeoutSeconds > 0 ? requestTimeoutSeconds : 60;
        EnableToolCalling       = enableToolCalling;
        EnableStructuredOutput  = enableStructuredOutput;
        Temperature             = temperature;
        TopP                    = topP;
        MaxTokens               = maxTokens;
        VisionModelName         = string.IsNullOrWhiteSpace(visionModelName) ? null : visionModelName.Trim();
        ImageGenerationModelName = string.IsNullOrWhiteSpace(imageGenerationModelName) ? null : imageGenerationModelName.Trim();
        MessagingModelName       = string.IsNullOrWhiteSpace(messagingModelName) ? null : messagingModelName.Trim();
        UpdatedAt               = DateTime.UtcNow;
    }
}
