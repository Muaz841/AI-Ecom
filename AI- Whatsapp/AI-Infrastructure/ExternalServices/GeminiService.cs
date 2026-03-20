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
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Classify the customer intent from this message. Return only one word: greeting | order_start | inquiry | complaint | unhandled.\nMessage: {request.MessageContent}";
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
        var prompt = $"Reply as polite ecommerce support.\nMessage: {request.MessageContent}\nIntent: {request.DetectedIntent}\nInventory: {request.InventoryContext}";
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
        var prompt = $"Generate a caption for {request.ProductName}, description {request.ProductDescription}, price {request.Price} {request.Currency}, style {request.StylePreferences}.";
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
        var prompt = $"Generate {request.VariationCount} ad copy variations for {request.ProductName}. Description: {request.Description}. Price: {request.Price}. One per line.";
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
        return ("Google Gemini", rtConfig?.GeminiModel ?? "not set");
    }

    // ── Native agent turn (multi-turn function calling) ────────────────────────

    public async Task<AgentTurnResult> GenerateAgentTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var rt      = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken)
                      ?? throw new InvalidOperationException("AI provider is not configured. Configure it in Host > AI Settings.");
        var apiKey  = rt.GeminiApiKey;
        var model   = rt.GeminiModel;
        var timeout = rt.RequestTimeoutSeconds;

        if (string.IsNullOrWhiteSpace(model))
            throw new AiModelNotConfiguredException();

        EnsureApiKey(apiKey, "Gemini");

        var systemInstruction = BuildSystemInstruction(request.SystemPrompt);
        var generationConfig  = BuildGenerationConfig(rt);
        var contents = request.Contents.Select(BuildGeminiContentPart).ToArray();

        object[]? tools = null;
        if (request.Tools.Count > 0)
        {
            var declarations = request.Tools
                .Select(t => new
                {
                    name        = t.Name,
                    description = t.Description,
                    parameters  = ParseJsonOrEmpty(t.ParametersSchema),
                })
                .ToArray();
            tools = [new { functionDeclarations = declarations }];
        }

        var body = BuildAgentRequestBody(systemInstruction, contents, generationConfig, tools);
        _logger.Info("Gemini request payload (agent): {Payload}", SafeSerialize(body));

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(timeout);

        var path = $"v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey!)}";
        using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Gemini agent turn failed. Model={Model} Status={Status} Body={Body}",
                model, response.StatusCode, raw);
            throw new InvalidOperationException($"Gemini request failed ({response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(raw);
        var usage = ReadUsage(doc.RootElement, string.Empty);

        var candidatesEl = doc.RootElement.GetProperty("candidates");
        if (candidatesEl.GetArrayLength() == 0)
            return new AgentTurnResult(false, null, null, usage.InputTokens, usage.OutputTokens,
                "Gemini returned no candidates.");

        var partsEl = candidatesEl[0].GetProperty("content").GetProperty("parts");
        if (partsEl.GetArrayLength() == 0)
            return new AgentTurnResult(false, null, null, usage.InputTokens, usage.OutputTokens,
                "Gemini returned empty parts.");

        var firstPart = partsEl[0];

        if (firstPart.TryGetProperty("functionCall", out var funcCallEl))
        {
            var toolName = funcCallEl.GetProperty("name").GetString() ?? string.Empty;
            var argsJson = funcCallEl.TryGetProperty("args", out var argsEl)
                ? argsEl.GetRawText()
                : "{}";

            _logger.Info("Gemini native functionCall: {Tool} | model={Model}", toolName, model);
            return new AgentTurnResult(true, null, new ToolCall(toolName, argsJson),
                usage.InputTokens, usage.OutputTokens);
        }

        var text = firstPart.TryGetProperty("text", out var textEl)
            ? textEl.GetString() ?? string.Empty
            : string.Empty;

        _logger.Info("Gemini agent turn final. Model={Model} Tokens: in={In} out={Out}",
            model, usage.InputTokens, usage.OutputTokens);

        return new AgentTurnResult(true, text.Trim(), null, usage.InputTokens, usage.OutputTokens);
    }

    // ── Core execution ────────────────────────────────────────────────────────

    private async Task<T> ExecutePromptAsync<T>(
        string prompt,
        string? tenantSystemPrompt,
        Func<string, TokenUsage, T> parse,
        CancellationToken cancellationToken)
    {
        var rt      = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken)
                      ?? throw new InvalidOperationException("AI provider is not configured. Configure it in Host > AI Settings.");
        var apiKey  = rt.GeminiApiKey;
        var model   = rt.GeminiModel;
        var timeout = rt.RequestTimeoutSeconds;

        if (string.IsNullOrWhiteSpace(model))
            throw new AiModelNotConfiguredException();

        EnsureApiKey(apiKey, "Gemini");

        var systemInstruction = BuildSystemInstruction(tenantSystemPrompt);
        var generationConfig  = BuildGenerationConfig(rt);
        var body = BuildRequestBody(prompt, systemInstruction, generationConfig);
        _logger.Info("Gemini request payload (text): {Payload}", SafeSerialize(body));

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(timeout);

        var path = $"v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey!)}";
        using var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error("Gemini request failed. Model={Model} Status={Status} Body={Body}", model, response.StatusCode, raw);
            throw new InvalidOperationException($"Gemini request failed ({response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var usage = ReadUsage(doc.RootElement, prompt);

        _logger.Info(
            "Gemini call completed. Model={Model} PromptTokens={In} OutputTokens={Out} TotalTokens={Total}",
            model, usage.InputTokens, usage.OutputTokens, usage.InputTokens + usage.OutputTokens);

        return parse(text, usage);
    }
    

    private static string BuildSystemInstruction(string? tenantSystemPrompt)
    {
        var base_ = string.IsNullOrWhiteSpace(tenantSystemPrompt)
            ? string.Empty
            : tenantSystemPrompt.Trim();
        return base_ + SafetyPolicy;
    }

    private static Dictionary<string, object>? BuildGenerationConfig(AiRuntimeConfig? rt)
    {
        if (rt is null) return null;
        var config = new Dictionary<string, object>();
        if (rt.Temperature is not null) config["temperature"]      = rt.Temperature.Value;
        if (rt.TopP is not null)        config["topP"]             = rt.TopP.Value;
        if (rt.MaxTokens is not null)   config["maxOutputTokens"]  = rt.MaxTokens.Value;
        if (rt.EnableStructuredOutput)  config["responseMimeType"] = "application/json";
        return config.Count > 0 ? config : null;
    }

    private static object BuildRequestBody(
        string prompt,
        string systemInstruction,
        Dictionary<string, object>? generationConfig)
    {
        var contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } };
        var sysInstruction = new { parts = new[] { new { text = systemInstruction } } };

        return generationConfig is not null
            ? new { systemInstruction = sysInstruction, generationConfig, contents }
            : (object)new { systemInstruction = sysInstruction, contents };
    }

    private static object BuildGeminiContentPart(AgentContentPart part)
    {
        if (part.Text is not null)
            return new { role = part.Role, parts = new[] { new { text = part.Text } } };

        if (part.FunctionCall is not null)
        {
            var args = ParseJsonOrEmpty(part.FunctionCall.ArgumentsJson);
            return new { role = "model", parts = new[] { new { functionCall = new { name = part.FunctionCall.ToolName, args } } } };
        }

        if (part.FunctionResponse is not null)
        {
            var responseObj = ParseJsonOrEmpty(part.FunctionResponse.ResultJson);
            return new { role = "user", parts = new[] { new { functionResponse = new { name = part.FunctionResponse.ToolName, response = responseObj } } } };
        }

        throw new InvalidOperationException("AgentContentPart must have Text, FunctionCall, or FunctionResponse.");
    }

    private static object BuildAgentRequestBody(
        string systemInstruction,
        object[] contents,
        Dictionary<string, object>? generationConfig,
        object[]? tools)
    {
        var sysInstr = new { parts = new[] { new { text = systemInstruction } } };

        return (tools, generationConfig) switch
        {
            ({ } t, { } gc) => new { systemInstruction = sysInstr, tools = t, generationConfig = gc, contents },
            ({ } t, null)   => new { systemInstruction = sysInstr, tools = t, contents },
            (null, { } gc)  => new { systemInstruction = sysInstr, generationConfig = gc, contents },
            _               => (object)new { systemInstruction = sysInstr, contents },
        };
    }

    private static object ParseJsonOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new { };
        try   { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return new { }; }
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

    private static string SafeSerialize(object body)
    {
        try
        {
            return JsonSerializer.Serialize(body);
        }
        catch
        {
            return "[unserializable]";
        }
    }

    private readonly record struct TokenUsage(int InputTokens, int OutputTokens);
}
