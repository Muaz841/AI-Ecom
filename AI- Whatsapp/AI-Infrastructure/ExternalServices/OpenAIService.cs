using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class OpenAIService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AISettings _settings;
    private readonly IAiRuntimeConfigProvider _runtimeConfig;
    private readonly IApplicationLogger _logger;

    public OpenAIService(
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
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildIntentPrompt(request);
        return ExecutePromptAsync(
            prompt,
            request.SystemPrompt,
            (text, usage) => new IntentDetectionResult(text.Trim(), 0.85, prompt, text, usage.InputTokens, usage.OutputTokens),
            cancellationToken);
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildReplyPrompt(request);
        return ExecutePromptAsync(
            prompt,
            request.SystemPrompt,
            (text, usage) => new ReplyGenerationResult(true, text.Trim(), prompt, text, usage.InputTokens, usage.OutputTokens, false),
            cancellationToken);
    }

    public Task<CaptionGenerationResult> GenerateCaptionAsync(
        CaptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Write an ecommerce caption for {request.ProductName}. Description: {request.ProductDescription}. Price: {request.Price} {request.Currency}. Style: {request.StylePreferences}. Limit {request.MaxLength} chars. Include 3 hashtags in separate line prefixed by #.";
        return ExecutePromptAsync(
            prompt,
            null,
            (text, usage) => new CaptionGenerationResult(true, text.Trim(), ExtractHashtags(text), prompt, text, usage.InputTokens, usage.OutputTokens),
            cancellationToken);
    }

    public Task<AdCopiesResult> GenerateAdCopiesAsync(
        AdRequest request,
        bool estimateTokensOnly = false,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Generate {request.VariationCount} ad copy variations for product {request.ProductName}. Description: {request.Description}. Price: {request.Price}. Return one variation per line.";
        var estimate = EstimateTokens(prompt);

        if (estimateTokensOnly)
            return Task.FromResult(new AdCopiesResult(true, Array.Empty<string>(), prompt, "[estimate-only]", 0, 0, estimate));

        return ExecutePromptAsync(
            prompt,
            null,
            (text, usage) => new AdCopiesResult(true, SplitLines(text), prompt, text, usage.InputTokens, usage.OutputTokens, estimate),
            cancellationToken);
    }

    public async Task<(string ProviderName, string ModelVersion)> GetCurrentProviderInfoAsync(
        CancellationToken cancellationToken = default)
    {
        var rtConfig = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken);
        return ("OpenAI", rtConfig?.OpenAIModel ?? "not set");
    }

    public Task<AgentTurnResult> GenerateAgentTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var userText = request.Contents
            .LastOrDefault(c => c.Role == "user" && c.Text is not null)?.Text ?? string.Empty;

        var replyRequest = new ReplyRequest(userText, "inquiry", string.Empty, "agent", request.SystemPrompt);
        return GenerateReplyAsync(replyRequest, cancellationToken)
            .ContinueWith(t =>
            {
                var r = t.Result;
                return new AgentTurnResult(r.Success, r.GeneratedReply, null,
                    r.InputTokensUsed, r.OutputTokensUsed, r.ErrorMessage);
            }, TaskScheduler.Default);
    }

    private async Task<T> ExecutePromptAsync<T>(
        string prompt,
        string? systemPrompt,
        Func<string, TokenUsage, T> parse,
        CancellationToken cancellationToken)
    {
        var rt      = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken)
                      ?? throw new InvalidOperationException("AI provider is not configured. Configure it in Host > AI Settings.");
        var apiKey  = rt.OpenAIApiKey;
        var model   = rt.OpenAIModel;
        var timeout = rt.RequestTimeoutSeconds;

        EnsureApiKey(apiKey, "OpenAI");

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromSeconds(timeout);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var messages = string.IsNullOrWhiteSpace(systemPrompt)
            ? (object)new[] { new { role = "user", content = prompt } }
            : (object)new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = prompt }
            };

        var body = new { model, messages, temperature = 0.2 };

        using var response = await client.PostAsJsonAsync("v1/chat/completions", body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("OpenAI request failed. Status={Status}, Body={Body}", response.StatusCode, raw);
            throw new InvalidOperationException("OpenAI request failed.");
        }

        using var doc = JsonDocument.Parse(raw);
        var text  = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var usage = ReadUsage(doc.RootElement);

        _logger.Info(
            "OpenAI prompt={Prompt} response={Response} inputTokens={InputTokens} outputTokens={OutputTokens}",
            prompt, text, usage.InputTokens, usage.OutputTokens);

        return parse(text, usage);
    }

    private static TokenUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage)) return new TokenUsage(0, 0);
        var input  = usage.TryGetProperty("prompt_tokens",     out var p) ? p.GetInt32() : 0;
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

    private static void EnsureApiKey(string? apiKey, string provider)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"{provider} API key is missing.");
    }

    private static string BuildIntentPrompt(IntentRequest r)
        => $"Classify the customer intent from this {r.Platform} message. Return only one word: greeting, order_start, inquiry, complaint, unhandled.\nMessage: {r.MessageContent}";

    private static string BuildReplyPrompt(ReplyRequest r)
        => $"You are a helpful fashion store assistant.\nCustomer message: {r.MessageContent}\nIntent: {r.DetectedIntent}\nInventory: {r.InventoryContext}\nReply in concise customer-care tone.";

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
