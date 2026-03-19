using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class AIServiceFactory : IAIService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAiRuntimeConfigProvider _runtimeConfigProvider;

    public AIServiceFactory(
        IServiceProvider serviceProvider,
        IAiRuntimeConfigProvider runtimeConfigProvider)
    {
        _serviceProvider       = serviceProvider       ?? throw new ArgumentNullException(nameof(serviceProvider));
        _runtimeConfigProvider = runtimeConfigProvider ?? throw new ArgumentNullException(nameof(runtimeConfigProvider));
    }
    

    private async Task<IAIService> GetActiveProviderAsync(CancellationToken cancellationToken)
    {
        var rtConfig = await _runtimeConfigProvider.GetRuntimeConfigAsync(cancellationToken);
        if (rtConfig is null)
            throw new InvalidOperationException("AI provider is not configured. Configure it in Host > AI Settings.");

        return rtConfig.ActiveProvider switch
        {
            AIProvider.OpenAI => _serviceProvider.GetRequiredService<OpenAIService>(),
            AIProvider.Gemini => _serviceProvider.GetRequiredService<GeminiService>(),
            AIProvider.Ollama => _serviceProvider.GetRequiredService<OllamaService>(),
            _                 => throw new InvalidOperationException($"Unsupported AI provider: {rtConfig.ActiveProvider}")
        };
    }

    // ── IAIService delegation ─────────────────────────────────────────────────

    public async Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(cancellationToken);
        return await provider.DetectIntentAsync(request, cancellationToken);
    }

    public async Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(cancellationToken);
        return await provider.GenerateReplyAsync(request, cancellationToken);
    }

    public async Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(cancellationToken);
        return await provider.GenerateCaptionAsync(request, cancellationToken);
    }

    public async Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(cancellationToken);
        return await provider.GenerateAdCopiesAsync(request, estimateTokensOnly, cancellationToken);
    }

    public async Task<AgentTurnResult> GenerateAgentTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(cancellationToken);
        return await provider.GenerateAgentTurnAsync(request, cancellationToken);
    }

    public async Task<(string ProviderName, string ModelVersion)> GetCurrentProviderInfoAsync(
        CancellationToken cancellationToken = default)
    {
        var rtConfig = await _runtimeConfigProvider.GetRuntimeConfigAsync(cancellationToken);
        if (rtConfig is null) return ("Not Configured", "n/a");

        return rtConfig.ActiveProvider switch
        {
            AIProvider.OpenAI => ("OpenAI", rtConfig.OpenAIModel ?? "not set"),
            AIProvider.Gemini => ("Google Gemini", rtConfig.GeminiModel ?? "not set"),
            AIProvider.Ollama => ("Ollama", rtConfig.OllamaModel),
            _                 => (rtConfig.ActiveProvider.ToString(), "unknown")
        };
    }
}

public class AISettings
{
    public bool   DebugModeEnabled      { get; set; } = false;
    public string OllamaEndpoint        { get; set; } = "http://localhost:11434";
    public string OllamaModel           { get; set; } = "llama3.1:8b";
    public string OpenAIModel           { get; set; } = "gpt-4o-mini";
    public string GeminiModel           { get; set; } = "gemini-1.5-flash";
    public int    RequestTimeoutSeconds { get; set; } = 60;
    public string OpenAIApiKey          { get; set; } = string.Empty;
    public string GeminiApiKey          { get; set; } = string.Empty;
}
