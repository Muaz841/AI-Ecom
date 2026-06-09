using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// Calls Gemini text-embedding-004 to produce 768-dim float vectors.
/// Uses the platform Gemini API key from IAiRuntimeConfigProvider.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private const string EmbeddingModel   = "text-embedding-004";
    private const string GeminiBaseUrl    = "https://generativelanguage.googleapis.com/";
    private const int    TimeoutSeconds   = 30;

    private readonly IHttpClientFactory          _httpClientFactory;
    private readonly IAiRuntimeConfigProvider    _runtimeConfig;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IAiRuntimeConfigProvider runtimeConfig,
        ILogger<GeminiEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _runtimeConfig     = runtimeConfig;
        _logger            = logger;
    }

    public async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var rt = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken);
            var apiKey = rt?.GeminiApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Gemini API key not configured — skipping embedding.");
                return null;
            }

            var body = new
            {
                model = $"models/{EmbeddingModel}",
                content = new { parts = new[] { new { text } } }
            };

            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(GeminiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            var path = $"v1beta/models/{EmbeddingModel}:embedContent?key={Uri.EscapeDataString(apiKey)}";
            using var response = await client.PostAsJsonAsync(path, body, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini embedding failed. Status={Status} Body={Body}", response.StatusCode, error);
                return null;
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(raw);

            if (!doc.RootElement.TryGetProperty("embedding", out var embeddingEl) ||
                !embeddingEl.TryGetProperty("values", out var valuesEl))
            {
                _logger.LogError("Gemini embedding response missing expected fields. Body={Body}", raw);
                return null;
            }

            var count  = valuesEl.GetArrayLength();
            var vector = new float[count];
            var i = 0;
            foreach (var element in valuesEl.EnumerateArray())
                vector[i++] = element.GetSingle();

            _logger.LogDebug("Embedding created. Dims={Dims} TextLength={Length}", count, text.Length);
            return vector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in GeminiEmbeddingService.GetEmbeddingAsync.");
            return null;
        }
    }
}
