using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class OpenAIService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AISettings _settings;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(
        IHttpClientFactory httpClientFactory,
        IOptions<AISettings> settings,
        ILogger<OpenAIService> logger)
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
        var prompt = BuildIntentPrompt(request);
        return ExecutePromptAsync(
            prompt,
            simulatedResultFactory: () => new IntentDetectionResult("inquiry", 0.95, prompt, "[simulated]", 10, 6, true),
            parse: (responseText, usage, wasSimulated) => new IntentDetectionResult(
                responseText.Trim(),
                0.85,
                prompt,
                responseText,
                usage.InputTokens,
                usage.OutputTokens,
                wasSimulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildReplyPrompt(request);
        return ExecutePromptAsync(
            prompt,
            simulatedResultFactory: () => new ReplyGenerationResult(
                true,
                "Thanks for your message. We will assist you shortly.",
                prompt,
                "[simulated]",
                16,
                18,
                false,
                true),
            parse: (responseText, usage, wasSimulated) => new ReplyGenerationResult(
                true,
                responseText.Trim(),
                prompt,
                responseText,
                usage.InputTokens,
                usage.OutputTokens,
                false,
                wasSimulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        bool simulateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Write an ecommerce caption for {request.ProductName}. Description: {request.ProductDescription}. Price: {request.Price} {request.Currency}. Style: {request.StylePreferences}. Limit {request.MaxLength} chars. Include 3 hashtags in separate line prefixed by #.";
        return ExecutePromptAsync(
            prompt,
            simulatedResultFactory: () => new CaptionGenerationResult(
                true,
                $"New arrival: {request.ProductName} at {request.Price} {request.Currency}. Grab yours now!",
                new[] { "#NewArrival", "#ShopNow", "#Fashion" },
                prompt,
                "[simulated]",
                20,
                25,
                true),
            parse: (responseText, usage, wasSimulated) => new CaptionGenerationResult(
                true,
                responseText.Trim(),
                ExtractHashtags(responseText),
                prompt,
                responseText,
                usage.InputTokens,
                usage.OutputTokens,
                wasSimulated),
            simulateOnly,
            cancellationToken);
    }

    public Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool simulateOnly = false,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Generate {request.VariationCount} ad copy variations for product {request.ProductName}. Description: {request.Description}. Price: {request.Price}. Return one variation per line.";
        var estimate = EstimateTokens(prompt);

        if (estimateTokensOnly)
        {
            return Task.FromResult(new AdCopiesResult(
                true,
                Array.Empty<string>(),
                prompt,
                "[estimate-only]",
                0,
                0,
                estimate,
                true));
        }

        return ExecutePromptAsync(
            prompt,
            simulatedResultFactory: () => new AdCopiesResult(
                true,
                new[] { "Fast delivery. Limited stock.", "Upgrade your style today.", "Shop now before it sells out." },
                prompt,
                "[simulated]",
                24,
                32,
                estimate,
                true),
            parse: (responseText, usage, wasSimulated) => new AdCopiesResult(
                true,
                SplitLines(responseText),
                prompt,
                responseText,
                usage.InputTokens,
                usage.OutputTokens,
                estimate,
                wasSimulated),
            simulateOnly,
            cancellationToken);
    }

    public (string ProviderName, string ModelVersion) GetCurrentProviderInfo()
        => ("OpenAI", _settings.OpenAIModel);

    private async Task<T> ExecutePromptAsync<T>(
        string prompt,
        Func<T> simulatedResultFactory,
        Func<string, TokenUsage, bool, T> parse,
        bool simulateOnly,
        CancellationToken cancellationToken)
    {
        if (simulateOnly)
        {
            _logger.LogDebug("OpenAI simulate-only prompt: {Prompt}", prompt);
            return simulatedResultFactory();
        }

        EnsureApiKey(_settings.OpenAIApiKey, "OpenAI");

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenAIApiKey);

        var body = new
        {
            model = _settings.OpenAIModel,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.2
        };

        using var response = await client.PostAsJsonAsync("v1/chat/completions", body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI request failed. Status={Status}, Body={Body}", response.StatusCode, raw);
            throw new InvalidOperationException("OpenAI request failed.");
        }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var usage = ReadUsage(doc.RootElement);

        _logger.LogDebug(
            "OpenAI prompt={Prompt} response={Response} inputTokens={InputTokens} outputTokens={OutputTokens}",
            prompt,
            text,
            usage.InputTokens,
            usage.OutputTokens);

        return parse(text, usage, false);
    }

    private static TokenUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage))
        {
            return new TokenUsage(0, 0);
        }

        var input = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
        var output = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        return new TokenUsage(input, output);
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    private static string[] SplitLines(string text)
        => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] ExtractHashtags(string text)
        => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void EnsureApiKey(string apiKey, string provider)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"{provider} API key is missing.");
        }
    }

    private static string BuildIntentPrompt(IntentRequest r)
        => $"Classify intent from this {r.Platform} customer message.\nMessage: {r.MessageContent}\nInventory: {r.InventoryContext}\nAllowed intents: greeting, order_start, inquiry, complaint, unhandled.\nReturn only one intent word.";

    private static string BuildReplyPrompt(ReplyRequest r)
        => $"You are a helpful fashion store assistant.\nCustomer message: {r.MessageContent}\nIntent: {r.DetectedIntent}\nInventory: {r.InventoryContext}\nReply in concise customer-care tone.";

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
