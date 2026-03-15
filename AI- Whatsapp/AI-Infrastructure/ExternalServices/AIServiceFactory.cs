using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class AIServiceFactory : IAIService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MockAIService _mockService;
    private readonly AISettings _fallbackSettings;
    private readonly IAiRuntimeConfigProvider _runtimeConfigProvider;
    private readonly IApplicationLogger _logger;

    public AIServiceFactory(
        IServiceProvider serviceProvider,
        IOptions<AISettings> fallbackSettings,
        IAiRuntimeConfigProvider runtimeConfigProvider,
        IApplicationLogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _fallbackSettings = fallbackSettings?.Value ?? throw new ArgumentNullException(nameof(fallbackSettings));
        _runtimeConfigProvider = runtimeConfigProvider ?? throw new ArgumentNullException(nameof(runtimeConfigProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mockService = serviceProvider.GetRequiredService<MockAIService>();
    }

    // ── Provider resolution (DB-first, fallback to appsettings) ──────────────

    private async Task<IAIService> GetActiveProviderAsync(bool simulateOnly, CancellationToken cancellationToken)
    {
        var rtConfig = await _runtimeConfigProvider.GetRuntimeConfigAsync(cancellationToken);

        var effectiveDebug = rtConfig?.DebugModeEnabled ?? _fallbackSettings.DebugModeEnabled;
        var effectiveProvider = ParseProvider(rtConfig?.ActiveProvider) ?? _fallbackSettings.Provider;

        if (simulateOnly || effectiveDebug)
        {
            _logger.Info(
                "AI call routed to MOCK provider (simulateOnly={SimulateOnly}, DebugMode={DebugMode})",
                simulateOnly,
                effectiveDebug);
            return _mockService;
        }

        return effectiveProvider switch
        {
            AIProvider.OpenAI => _serviceProvider.GetRequiredService<OpenAIService>(),
            AIProvider.Gemini => _serviceProvider.GetRequiredService<GeminiService>(),
            AIProvider.Ollama => _serviceProvider.GetRequiredService<OllamaService>(),
            _ => throw new InvalidOperationException($"Unsupported AI provider: {effectiveProvider}")
        };
    }

    // ── IAIService delegation ─────────────────────────────────────────────────

    public async Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(simulateOnly, cancellationToken);
        return await provider.DetectIntentAsync(request, simulateOnly, cancellationToken);
    }

    public async Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(simulateOnly, cancellationToken);
        return await provider.GenerateReplyAsync(request, simulateOnly, cancellationToken);
    }

    public async Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(simulateOnly, cancellationToken);
        return await provider.GenerateCaptionAsync(request, simulateOnly, cancellationToken);
    }

    public async Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool simulateOnly = false,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetActiveProviderAsync(simulateOnly, cancellationToken);
        return await provider.GenerateAdCopiesAsync(request, simulateOnly, estimateTokensOnly, cancellationToken);
    }

    public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
    {
        // Synchronous best-effort using fallback settings only.
        var realInfo = _fallbackSettings.Provider switch
        {
            AIProvider.OpenAI => ("OpenAI", _fallbackSettings.OpenAIModel),
            AIProvider.Gemini => ("Gemini", _fallbackSettings.GeminiModel),
            AIProvider.Ollama => ("Ollama", _fallbackSettings.OllamaModel),
            _ => ("Unknown", "n/a")
        };

        return _fallbackSettings.DebugModeEnabled
            ? ($"DEBUG-MOCK-{realInfo.Item1}", "mock-v1")
            : realInfo;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AIProvider? ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<AIProvider>(value, ignoreCase: true, out var result) ? result : null;
    }
}

public class AISettings
{
    public AIProvider Provider { get; set; } = AIProvider.Ollama;
    public bool DebugModeEnabled { get; set; } = true;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.1:8b";
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
    public string GeminiModel { get; set; } = "gemini-1.5-flash";
    public int RequestTimeoutSeconds { get; set; } = 60;
    public string OpenAIApiKey { get; set; } = string.Empty;
    public string GeminiApiKey { get; set; } = string.Empty;
}

public enum AIProvider
{
    OpenAI,
    Gemini,
    Ollama
}
