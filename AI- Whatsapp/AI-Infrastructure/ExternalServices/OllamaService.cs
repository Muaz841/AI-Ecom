using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Constants;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public class OllamaService : IAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AISettings _settings;
    private readonly IAiRuntimeConfigProvider _runtimeConfig;
    private readonly IApplicationLogger _logger;

    public OllamaService(
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
        var prompt = $"Classify the customer intent. Return only one word: {string.Join(", ", AiIntentCodes.All)}.\nMessage: {request.MessageContent}";
        return ExecutePromptAsync(
            prompt,
            request.SystemPrompt,
            (text, usage) => new IntentDetectionResult(text.Trim(), 0.82, prompt, text, usage.InputTokens, usage.OutputTokens),
            cancellationToken);
    }

    public Task<ReplyGenerationResult> GenerateReplyAsync(
        ReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Reply politely to customer.\nMessage: {request.MessageContent}\nIntent: {request.DetectedIntent}\nInventory: {request.InventoryContext}";
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
        var prompt = $"Create an Instagram/Facebook caption for {request.ProductName}. Description: {request.ProductDescription}. Price: {request.Price} {request.Currency}. Style: {request.StylePreferences}.";
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
        var prompt = $"Generate {request.VariationCount} ad copy variants for {request.ProductName}. Description: {request.Description}. Price: {request.Price}. One per line.";
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
        return ("Ollama", rtConfig?.OllamaModel ?? "not set");
    }

    public Task<AgentTurnResult> GenerateAgentTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var userText = request.Contents
            .LastOrDefault(c => c.Role == "user" && c.Text is not null)?.Text ?? string.Empty;

        var replyRequest = new ReplyRequest(userText, AiIntentCodes.Inquiry, string.Empty, "agent", request.SystemPrompt);
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
        var rt       = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken)
                       ?? throw new InvalidOperationException("AI provider is not configured. Configure it in Host > AI Settings.");
        var endpoint = rt.OllamaEndpoint;
        var model    = rt.OllamaModel;
        var timeout  = rt.RequestTimeoutSeconds;

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(endpoint);
        client.Timeout = TimeSpan.FromSeconds(timeout);

        object body = string.IsNullOrWhiteSpace(systemPrompt)
            ? new { model, prompt, stream = false }
            : new { model, system = systemPrompt, prompt, stream = false };

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

        return parse(text, usage);
    }

    private static TokenUsage ReadUsage(JsonElement root, string prompt, string completion)
    {
        var input  = root.TryGetProperty("prompt_eval_count", out var p) ? p.GetInt32() : EstimateTokens(prompt);
        var output = root.TryGetProperty("eval_count",        out var c) ? c.GetInt32() : EstimateTokens(completion);
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
