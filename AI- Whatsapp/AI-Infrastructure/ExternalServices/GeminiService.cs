using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Common;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class GeminiService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AISettings _settings;
    private readonly IAiRuntimeConfigProvider _runtimeConfig;
    private readonly IApplicationLogger _logger;

    public GeminiService(
        IHttpClientFactory httpClientFactory,
        IOptions<AISettings> settings,
        IAiRuntimeConfigProvider runtimeConfig,
        IApplicationLogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _runtimeConfig = runtimeConfig;
        _logger = logger;
    }

    public Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Analyze customer intent. Message: {request.MessageContent}. Inventory: {request.InventoryContext}. Return only: greeting | order_start | inquiry | complaint | unhandled.";
        return ExecutePromptAsync(
            prompt,
            request.SystemPrompt,
            () => new IntentDetectionResult("inquiry", 0.93, prompt, "[simulated]", 11, 6, true),
            (text, usage, simulated) => new IntentDetectionResult(text.Trim(), 0.85, prompt, text, usage.InputTokens, usage.OutputTokens, simulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Reply as polite ecommerce support.\nMessage: {request.MessageContent}\nIntent: {request.DetectedIntent}\nInventory: {request.InventoryContext}";
        return ExecutePromptAsync(
            prompt,
            request.SystemPrompt,
            () => new ReplyGenerationResult(true, "Thanks for contacting us. We will help you right away.", prompt, "[simulated]", 18, 16, false, true),
            (text, usage, simulated) => new ReplyGenerationResult(true, text.Trim(), prompt, text, usage.InputTokens, usage.OutputTokens, false, simulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Generate a caption for {request.ProductName}, description {request.ProductDescription}, price {request.Price} {request.Currency}, style {request.StylePreferences}.";
        return ExecutePromptAsync(
            prompt,
            null,
            () => new CaptionGenerationResult(true, $"Introducing {request.ProductName}. Shop now!", new[] { "#Style", "#ShopNow", "#New" }, prompt, "[simulated]", 20, 18, true),
            (text, usage, simulated) => new CaptionGenerationResult(true, text.Trim(), ExtractHashtags(text), prompt, text, usage.InputTokens, usage.OutputTokens, simulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool simulateOnly = false,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Generate {request.VariationCount} ad copy variations for {request.ProductName}. Description: {request.Description}. Price: {request.Price}. One per line.";
        var estimate = EstimateTokens(prompt);
        if (estimateTokensOnly)
        {
            return Task.FromResult(new AdCopiesResult(true, Array.Empty<string>(), prompt, "[estimate-only]", 0, 0, estimate, true));
        }

        return ExecutePromptAsync(
            prompt,
            null,
            () => new AdCopiesResult(true, new[] { "Ad copy 1", "Ad copy 2", "Ad copy 3" }, prompt, "[simulated]", 20, 30, estimate, true),
            (text, usage, simulated) => new AdCopiesResult(true, SplitLines(text), prompt, text, usage.InputTokens, usage.OutputTokens, estimate, simulated),
            simulateOnly,
            cancellationToken);
    }

    public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
        => ("Google Gemini", _settings.GeminiModel);

    private async Task<T> ExecutePromptAsync<T>(
        string prompt,
        string? systemPrompt,
        Func<T> simulatedResultFactory,
        Func<string, TokenUsage, bool, T> parse,
        bool simulateOnly,
        CancellationToken cancellationToken)
    {
        if (simulateOnly)
        {
            _logger.Info("Gemini simulate-only prompt: {Prompt}", prompt);
            return simulatedResultFactory();
        }

        // Resolve effective config: DB-first, fallback to appsettings.
        var rt = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken);
        var apiKey = rt?.GeminiApiKey ?? _settings.GeminiApiKey;
        var model = rt?.GeminiModel ?? _settings.GeminiModel;
        var timeout = rt?.RequestTimeoutSeconds ?? _settings.RequestTimeoutSeconds;

        if (string.IsNullOrWhiteSpace(model))
            throw new AiModelNotConfiguredException();

        EnsureApiKey(apiKey, "Gemini");

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(timeout);

        var path = $"v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        // Build request body — include systemInstruction when a tenant system prompt is provided.
        object body = string.IsNullOrWhiteSpace(systemPrompt)
            ? new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            }
            : new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = prompt } } }
                }
            };

        using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Gemini request failed. Status={Status}, Body={Body}", response.StatusCode, raw);
            throw new InvalidOperationException("Gemini request failed.");
        }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var usage = ReadUsage(doc.RootElement, prompt);
        _logger.Info("Gemini prompt={Prompt} response={Response} in={Input} out={Output}", prompt, text, usage.InputTokens, usage.OutputTokens);

        return parse(text, usage, false);
    }

    private static TokenUsage ReadUsage(JsonElement root, string prompt)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage))
        {
            return new TokenUsage(EstimateTokens(prompt), 0);
        }

        var input = usage.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : EstimateTokens(prompt);
        var output = usage.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
        return new TokenUsage(input, output);
    }

    private static string[] SplitLines(string text)
        => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] ExtractHashtags(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    private static void EnsureApiKey(string apiKey, string provider)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"{provider} API key is missing.");
        }
    }

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
