using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class AIServiceFactory : IAIService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MockAIService _mockService;
    private readonly AISettings _settings;
    private readonly ILogger<AIServiceFactory> _logger;

    public AIServiceFactory(
        IServiceProvider serviceProvider,
        IOptions<AISettings> settings,
        ILogger<AIServiceFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mockService = serviceProvider.GetRequiredService<MockAIService>();

        _logger.LogInformation(
            "AI factory initialized. Active provider: {Provider} | Debug mode: {DebugMode}",
            _settings.Provider,
            _settings.DebugModeEnabled);
    }

    private IAIService GetActiveProvider(bool simulateOnly)
    {
        if (simulateOnly || _settings.DebugModeEnabled)
        {
            _logger.LogDebug(
                "AI call routed to MOCK provider (simulateOnly={SimulateOnly}, DebugMode={DebugMode})",
                simulateOnly,
                _settings.DebugModeEnabled);
            return _mockService;
        }

        return _settings.Provider switch
        {
            AIProvider.OpenAI => _serviceProvider.GetRequiredService<OpenAIService>(),
            AIProvider.Gemini => _serviceProvider.GetRequiredService<GeminiService>(),
            AIProvider.Ollama => _serviceProvider.GetRequiredService<OllamaService>(),
            _ => throw new InvalidOperationException($"Unsupported AI provider: {_settings.Provider}")
        };
    }

    public Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
        => GetActiveProvider(simulateOnly).DetectIntentAsync(request, simulateOnly, cancellationToken);

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
        => GetActiveProvider(simulateOnly).GenerateReplyAsync(request, simulateOnly, cancellationToken);

    public Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
        => GetActiveProvider(simulateOnly).GenerateCaptionAsync(request, simulateOnly, cancellationToken);

    public Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool simulateOnly = false,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
        => GetActiveProvider(simulateOnly).GenerateAdCopiesAsync(request, simulateOnly, estimateTokensOnly, cancellationToken);

    public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
    {
        var realInfo = _settings.Provider switch
        {
            AIProvider.OpenAI => ("OpenAI", _settings.OpenAIModel),
            AIProvider.Gemini => ("Gemini", _settings.GeminiModel),
            AIProvider.Ollama => ("Ollama", _settings.OllamaModel),
            _ => ("Unknown", "n/a")
        };

        return _settings.DebugModeEnabled
            ? ($"DEBUG-MOCK-{realInfo.Item1}", "mock-v1")
            : realInfo;
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
