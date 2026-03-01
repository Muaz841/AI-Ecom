using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class OllamaService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AISettings _settings;
    private readonly IApplicationLogger _logger;

    public OllamaService(
        IHttpClientFactory httpClientFactory,
        IOptions<AISettings> settings,
        IApplicationLogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;
    }

    public Task<IntentDetectionResult> DetectIntentAsync(
        IntentRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Classify customer intent.\nMessage: {request.MessageContent}\nInventory: {request.InventoryContext}\nReturn one: greeting, order_start, inquiry, complaint, unhandled.";
        return ExecutePromptAsync(
            prompt,
            () => new IntentDetectionResult("inquiry", 0.91, prompt, "[simulated]", 12, 8, true),
            (text, usage, simulated) => new IntentDetectionResult(text.Trim(), 0.82, prompt, text, usage.InputTokens, usage.OutputTokens, simulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Reply politely to customer.\nMessage: {request.MessageContent}\nIntent: {request.DetectedIntent}\nInventory: {request.InventoryContext}";
        return ExecutePromptAsync(
            prompt,
            () => new ReplyGenerationResult(true, "Thanks for your message. We can help you with that.", prompt, "[simulated]", 18, 20, false, true),
            (text, usage, simulated) => new ReplyGenerationResult(true, text.Trim(), prompt, text, usage.InputTokens, usage.OutputTokens, false, simulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Create an Instagram/Facebook caption for {request.ProductName}. Description: {request.ProductDescription}. Price: {request.Price} {request.Currency}. Style: {request.StylePreferences}.";
        return ExecutePromptAsync(
            prompt,
            () => new CaptionGenerationResult(true, $"New drop: {request.ProductName}. Shop now.", new[] { "#newdrop", "#style", "#shopnow" }, prompt, "[simulated]", 22, 20, true),
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
        var prompt = $"Generate {request.VariationCount} ad copy variants for {request.ProductName}. Description: {request.Description}. Price: {request.Price}. One per line.";
        var estimate = EstimateTokens(prompt);
        if (estimateTokensOnly)
        {
            return Task.FromResult(new AdCopiesResult(true, Array.Empty<string>(), prompt, "[estimate-only]", 0, 0, estimate, true));
        }

        return ExecutePromptAsync(
            prompt,
            () => new AdCopiesResult(true, new[] { "Limited stock, order now.", "Premium quality for everyday wear.", "Trending style, fast delivery." }, prompt, "[simulated]", 20, 24, estimate, true),
            (text, usage, simulated) => new AdCopiesResult(true, SplitLines(text), prompt, text, usage.InputTokens, usage.OutputTokens, estimate, simulated),
            simulateOnly,
            cancellationToken);
    }

    public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
        => ("Ollama", _settings.OllamaModel);

    private async Task<T> ExecutePromptAsync<T>(
        string prompt,
        Func<T> simulatedResultFactory,
        Func<string, TokenUsage, bool, T> parse,
        bool simulateOnly,
        CancellationToken cancellationToken)
    {
        if (simulateOnly)
        {
            _logger.Info("Ollama simulate-only prompt: {Prompt}", prompt);
            return simulatedResultFactory();
        }

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_settings.OllamaEndpoint);
        client.Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds);

        var body = new
        {
            model = _settings.OllamaModel,
            prompt,
            stream = false
        };

        using var response = await client.PostAsJsonAsync("api/generate", body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Ollama request failed. Status={Status}, Body={Body}", response.StatusCode, raw);
            throw new InvalidOperationException("Ollama request failed.");
        }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.TryGetProperty("response", out var reply)
            ? reply.GetString() ?? string.Empty
            : string.Empty;

        var usage = ReadUsage(doc.RootElement, prompt, text);
        _logger.Info("Ollama prompt={Prompt} response={Response} in={Input} out={Output}", prompt, text, usage.InputTokens, usage.OutputTokens);

        return parse(text, usage, false);
    }

    private static TokenUsage ReadUsage(JsonElement root, string prompt, string completion)
    {
        var input = root.TryGetProperty("prompt_eval_count", out var p)
            ? p.GetInt32()
            : EstimateTokens(prompt);
        var output = root.TryGetProperty("eval_count", out var c)
            ? c.GetInt32()
            : EstimateTokens(completion);
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

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
