using System;
using System.Collections.Generic;
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
    // ── Safety policy — always appended to every system instruction ───────────
    private const string SafetyPolicy =
        "\n\n[SAFETY POLICY] You must never produce content that is harmful, illegal, " +
        "deceptive, sexually explicit, or violates privacy. If a request asks for such " +
        "content, decline politely and redirect the conversation.";

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
            return Task.FromResult(new AdCopiesResult(true, Array.Empty<string>(), prompt, "[estimate-only]", 0, 0, estimate, true));

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

    // ── Core execution ────────────────────────────────────────────────────────

    private async Task<T> ExecutePromptAsync<T>(
        string prompt,
        string? tenantSystemPrompt,
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

        // ── 1. Resolve effective runtime config (DB-first, appsettings fallback) ──
        var rt = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken);
        var apiKey = rt?.GeminiApiKey ?? _settings.GeminiApiKey;
        var model  = rt?.GeminiModel  ?? _settings.GeminiModel;
        var timeout = rt?.RequestTimeoutSeconds ?? _settings.RequestTimeoutSeconds;

        // ── 2. Guard: model must be selected ──────────────────────────────────
        if (string.IsNullOrWhiteSpace(model))
            throw new AiModelNotConfiguredException();

        EnsureApiKey(apiKey, "Gemini");

        // ── 3. Build system instruction (tenant prompt + safety policy) ────────
        var systemInstruction = BuildSystemInstruction(tenantSystemPrompt);

        // ── 4. Build generationConfig (sampling + structured output) ──────────
        var generationConfig = BuildGenerationConfig(rt);

        // ── 5. Assemble request body ──────────────────────────────────────────
        var body = BuildRequestBody(prompt, systemInstruction, generationConfig);

        // ── 6. Send request ───────────────────────────────────────────────────
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(timeout);

        var path = $"v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Gemini request failed. Model={Model} Status={Status} Body={Body}", model, response.StatusCode, raw);
            throw new InvalidOperationException($"Gemini request failed ({response.StatusCode}).");
        }

        // ── 7. Parse response ─────────────────────────────────────────────────
        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var usage = ReadUsage(doc.RootElement, prompt);

        // ── 8. Observability — structured token log ───────────────────────────
        _logger.Info(
            "Gemini call completed. Model={Model} PromptTokens={In} OutputTokens={Out} TotalTokens={Total}",
            model, usage.InputTokens, usage.OutputTokens, usage.InputTokens + usage.OutputTokens);

        return parse(text, usage, false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the systemInstruction value.
    /// Tenant system prompt (brand voice, rules) is prepended; safety policy is always appended.
    /// </summary>
    private static string BuildSystemInstruction(string? tenantSystemPrompt)
    {
        var base_ = string.IsNullOrWhiteSpace(tenantSystemPrompt)
            ? string.Empty
            : tenantSystemPrompt.Trim();

        return base_ + SafetyPolicy;
    }

    /// <summary>
    /// Builds generationConfig from runtime settings.
    /// Temperature, TopP, MaxTokens are omitted when null — Gemini uses its defaults.
    /// ResponssMimeType is set to application/json when structured output is enabled.
    /// </summary>
    private static Dictionary<string, object>? BuildGenerationConfig(AiRuntimeConfig? rt)
    {
        if (rt is null) return null;

        var config = new Dictionary<string, object>();

        if (rt.Temperature is not null)
            config["temperature"] = rt.Temperature.Value;

        if (rt.TopP is not null)
            config["topP"] = rt.TopP.Value;

        if (rt.MaxTokens is not null)
            config["maxOutputTokens"] = rt.MaxTokens.Value;

        // Structured output — enforce JSON response format
        if (rt.EnableStructuredOutput)
            config["responseMimeType"] = "application/json";

        return config.Count > 0 ? config : null;
    }

    /// <summary>Assembles the full Gemini generateContent request body.</summary>
    private static object BuildRequestBody(
        string prompt,
        string systemInstruction,
        Dictionary<string, object>? generationConfig)
    {
        // Using anonymous objects — serialized by System.Text.Json via PostAsJsonAsync.
        // Omit null/empty optional fields to keep the request clean.
        var contents = new[]
        {
            new { role = "user", parts = new[] { new { text = prompt } } }
        };

        var sysInstruction = new { parts = new[] { new { text = systemInstruction } } };

        if (generationConfig is not null)
        {
            return new
            {
                systemInstruction = sysInstruction,
                generationConfig,
                contents
            };
        }

        return new
        {
            systemInstruction = sysInstruction,
            contents
        };
    }

    private static TokenUsage ReadUsage(JsonElement root, string prompt)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage))
            return new TokenUsage(EstimateTokens(prompt), 0);

        var input  = usage.TryGetProperty("promptTokenCount",     out var p) ? p.GetInt32() : EstimateTokens(prompt);
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

    private static void EnsureApiKey(string? apiKey, string provider)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"{provider} API key is missing.");
    }

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
