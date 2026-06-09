using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// Sends assembled context to the Claude Messages API and parses the JSON decision response.
/// Uses the Claude API key stored (encrypted) in PlatformMarketingConfig.
/// Falls back to no_action on any failure so the agent never hard-crashes.
/// </summary>
public sealed class ClaudeDecisionService : IClaudeDecisionService
{
    private const string ClaudeApiBaseUrl     = "https://api.anthropic.com/";
    private const string AnthropicVersion     = "2023-06-01";
    private const int    MaxOutputTokens      = 1024;
    private const int    TimeoutSeconds       = 60;

    private readonly IHttpClientFactory               _httpClientFactory;
    private readonly IPlatformMarketingConfigRepository _marketingConfigRepo;
    private readonly ITokenProtector                  _tokenProtector;
    private readonly ILogger<ClaudeDecisionService>   _logger;

    public ClaudeDecisionService(
        IHttpClientFactory httpClientFactory,
        IPlatformMarketingConfigRepository marketingConfigRepo,
        ITokenProtector tokenProtector,
        ILogger<ClaudeDecisionService> logger)
    {
        _httpClientFactory   = httpClientFactory;
        _marketingConfigRepo = marketingConfigRepo;
        _tokenProtector      = tokenProtector;
        _logger              = logger;
    }

    public async Task<ClaudeDecisionResult> DecideAsync(
        string systemPrompt,
        string userContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cfg = await _marketingConfigRepo.GetAsync(cancellationToken);
            if (cfg is null || !cfg.IsConfigured)
            {
                _logger.LogWarning("Marketing engine not configured — returning no_action.");
                return Fallback("Marketing engine not configured.");
            }

            var apiKey = _tokenProtector.Unprotect(cfg.ClaudeApiKeyProtected!);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Claude API key could not be decrypted — returning no_action.");
                return Fallback("API key decryption failed.");
            }

            var body = new
            {
                model      = cfg.ClaudeDecisionModel,
                max_tokens = MaxOutputTokens,
                system     = systemPrompt,
                messages   = new[]
                {
                    new { role = "user", content = userContext }
                }
            };

            using var client = _httpClientFactory.CreateClient("claude-decisions");
            client.BaseAddress = new Uri(ClaudeApiBaseUrl);
            client.Timeout     = TimeSpan.FromSeconds(TimeoutSeconds);
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

            using var response = await client.PostAsJsonAsync("v1/messages", body, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error. Status={Status} Body={Body}", response.StatusCode, raw);
                return Fallback($"Claude API returned {response.StatusCode}.");
            }

            return ParseDecision(raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ClaudeDecisionService.DecideAsync.");
            return Fallback(ex.Message);
        }
    }

    // ── Response parsing ────────────────────────────────────────────────────────

    private ClaudeDecisionResult ParseDecision(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var content   = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            // Extract JSON block from Claude's text response (handles markdown code fences)
            var json = ExtractJson(content);
            if (json is null)
            {
                _logger.LogWarning("Claude response did not contain a parseable JSON block. Raw={Raw}", content);
                return Fallback("No JSON in response.");
            }

            using var decisionDoc = JsonDocument.Parse(json);
            var root = decisionDoc.RootElement;

            var actionType       = root.TryGetProperty("action_type",        out var at)  ? at.GetString()  ?? "no_action" : "no_action";
            var actionPayload    = root.TryGetProperty("action_payload",      out var ap)  ? ap.GetRawText() : null;
            var reason           = root.TryGetProperty("reason",              out var r)   ? r.GetString()   : null;
            var confidence       = root.TryGetProperty("confidence",          out var c)   ? c.GetDouble()   : 0.5;
            var requiresApproval = root.TryGetProperty("requires_approval",   out var ra)  ? ra.GetBoolean() : true;

            _logger.LogInformation("Claude decision: {Action} (confidence={Confidence:P0})", actionType, confidence);
            return new ClaudeDecisionResult(actionType, actionPayload, reason, confidence, requiresApproval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response. Raw={Raw}", raw);
            return Fallback("Response parse error.");
        }
    }

    private static string? ExtractJson(string text)
    {
        // Try raw parse first
        text = text.Trim();
        if (text.StartsWith('{'))
        {
            try { JsonDocument.Parse(text); return text; } catch { }
        }

        // Try extracting from a ```json ... ``` code fence
        var fenceStart = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (fenceStart >= 0)
        {
            var jsonStart = text.IndexOf('\n', fenceStart) + 1;
            var fenceEnd  = text.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (fenceEnd > jsonStart)
                return text[jsonStart..fenceEnd].Trim();
        }

        // Try a plain ``` fence
        var plainStart = text.IndexOf("```", StringComparison.Ordinal);
        if (plainStart >= 0)
        {
            var jsonStart = text.IndexOf('\n', plainStart) + 1;
            var fenceEnd  = text.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (fenceEnd > jsonStart)
            {
                var candidate = text[jsonStart..fenceEnd].Trim();
                if (candidate.StartsWith('{')) return candidate;
            }
        }

        return null;
    }

    private static ClaudeDecisionResult Fallback(string reason) =>
        new("no_action", null, reason, 0.0, false);
}
