using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Constants;
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
        => $"Classify the customer intent from this {r.Platform} message. Return only one word: {string.Join(", ", AiIntentCodes.All)}.\nMessage: {r.MessageContent}";

    private static string BuildReplyPrompt(ReplyRequest r)
        => $"You are a helpful fashion store assistant.\nCustomer message: {r.MessageContent}\nIntent: {r.DetectedIntent}\nInventory: {r.InventoryContext}\nReply in concise customer-care tone.";

    // -- Pose extraction (vision) -----------------------------------------------

    public async Task<PoseExtractionResult> ExtractPoseAsync(
        PoseExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var rt = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken)
                 ?? throw new InvalidOperationException("AI provider is not configured.");

        EnsureApiKey(rt.OpenAIApiKey, "OpenAI");

        var model = !string.IsNullOrWhiteSpace(request.VisionModelOverride) ? request.VisionModelOverride
                  : !string.IsNullOrWhiteSpace(rt.VisionModelName)          ? rt.VisionModelName
                  : "gpt-4o";

        var timeout     = rt.RequestTimeoutSeconds;
        var imageBase64 = Convert.ToBase64String(request.ImageBytes);
        var dataUrl     = $"data:{request.MimeType};base64,{imageBase64}";

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("PoseExtractionPrompt is not configured. Please set it in AI Profile settings.");

        var posePrompt = request.Prompt;

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromSeconds(timeout);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rt.OpenAIApiKey);

        var body = new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = posePrompt },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            },
            max_tokens = 1000
        };

        using var response = await client.PostAsJsonAsync("v1/chat/completions", body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("OpenAI pose extraction failed. Status={Status} Body={Body}", response.StatusCode, raw);
            return new PoseExtractionResult(false, null, 0, 0, $"Pose extraction failed ({response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(raw);
        var text  = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var usage = ReadUsage(doc.RootElement);

        _logger.Info("OpenAI pose extraction complete. Model={Model} InputTokens={In} OutputTokens={Out}",
            model, usage.InputTokens, usage.OutputTokens);

        return new PoseExtractionResult(true, text.Trim(), usage.InputTokens, usage.OutputTokens);
    }

    // -- Model image generation --------------------------------------------------

    public async Task<ImageGenerationResult> GenerateModelImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var rt = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken)
                 ?? throw new InvalidOperationException("AI provider is not configured.");

        EnsureApiKey(rt.OpenAIApiKey, "OpenAI");

        var model = !string.IsNullOrWhiteSpace(request.ImageModelOverride)         ? request.ImageModelOverride
                  : !string.IsNullOrWhiteSpace(rt.ImageGenerationModelName)         ? rt.ImageGenerationModelName
                  : "dall-e-3";

        var timeout  = rt.RequestTimeoutSeconds;

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("ImageGenerationPrompt is not configured. Please set it in AI Profile settings.");

        var prompt = request.Prompt;

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromSeconds(Math.Max(timeout, 120));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rt.OpenAIApiKey);

        var body = new
        {
            model,
            prompt,
            n = 1,
            size = "1024x1024",
            response_format = "b64_json"
        };

        using var response = await client.PostAsJsonAsync("v1/images/generations", body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("OpenAI image generation failed. Status={Status} Body={Body}", response.StatusCode, raw);
            return new ImageGenerationResult(false, null, null, $"Image generation failed ({response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(raw);
        var b64 = doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString() ?? string.Empty;

        if (string.IsNullOrEmpty(b64))
            return new ImageGenerationResult(false, null, null, "No image data returned.");

        var bytes = Convert.FromBase64String(b64);
        _logger.Info("OpenAI image generation complete. Model={Model} Size={Size}bytes", model, bytes.Length);
        return new ImageGenerationResult(true, bytes, "image/png");
    }

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
