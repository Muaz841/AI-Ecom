using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Api.Webhooks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dev/webhooks")]
public class WebhookTestController : ControllerBase
{
    private readonly IWebhookProcessor        _processor;
    private readonly IWebHostEnvironment      _env;
    private readonly IAiRuntimeConfigProvider _runtimeConfig;

    public WebhookTestController(
        IWebhookProcessor processor,
        IWebHostEnvironment env,
        IAiRuntimeConfigProvider runtimeConfig)
    {
        _processor     = processor;
        _env           = env;
        _runtimeConfig = runtimeConfig;
    }

    [HttpPost("test")]
    public async Task<ActionResult<WebhookTestResponse>> SimulateWebhook(
        [FromBody] WebhookTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var platform    = request.Platform.ToLowerInvariant();
        var messageType = string.IsNullOrWhiteSpace(request.MessageType) ? "text" : request.MessageType.ToLowerInvariant();

        string payloadJson;
        if (request.UseRawPayload && !string.IsNullOrWhiteSpace(request.RawPayloadJson))
        {
            try { JsonDocument.Parse(request.RawPayloadJson); }
            catch { return BadRequest(new { error = "RawPayloadJson is not valid JSON." }); }
            payloadJson = request.RawPayloadJson;
        }
        else
        {
            payloadJson = BuildPayload(platform, request.From, request.To, request.Message, messageType);
        }
        
        var result = await _processor.ProcessAsync(
            rawJson:           payloadJson,
            skipSignature:     true,
            signatureHeader:   null,
            endpoint:          "/api/dev/webhooks/test",
            correlationId:     HttpContext.TraceIdentifier,
            cancellationToken: cancellationToken);


        var first    = result.MessageResults.Count > 0 ? result.MessageResults[0] : null;
        var rtConfig = await _runtimeConfig.GetRuntimeConfigAsync(cancellationToken);
        var provider = rtConfig?.ActiveProvider.ToString() ?? "Unknown";
        var model    = rtConfig?.ActiveProvider switch
        {
            EcomAI.Platform.Business.AIProvider.OpenAI => rtConfig.OpenAIModel ?? "not set",
            EcomAI.Platform.Business.AIProvider.Gemini => rtConfig.GeminiModel ?? "not set",
            EcomAI.Platform.Business.AIProvider.Ollama => rtConfig.OllamaModel,
            _ => "not set"
        }   ?? "not set";

        return Ok(new WebhookTestResponse(
            RequestPayload: payloadJson,
            StatusCode:     result.StatusCode,
            Success:        result.Success,
            ResultMessage:  result.Message,
            ProcessedCount: result.ProcessedCount,
            DetectedIntent: first?.DetectedIntent,
            ReplySent:      first?.ReplySent,
            ErrorMessage:   result.ErrorMessage ?? (result.Success ? null : first?.ErrorMessage),
            ToolCallsMade:  first?.ToolCallsMade?.ToList() ?? new List<string>(),
            InputTokens:    first?.InputTokens ?? 0,
            OutputTokens:   first?.OutputTokens ?? 0,
            AiProvider:     provider,
            AiModel:        model));
    }

   

    private static string BuildPayload(
        string platform, string from, string to, string message, string messageType)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fakeId    = $"test_{Guid.NewGuid():N}";

        return platform switch
        {
            "whatsapp" => BuildWhatsAppPayload(from, to, message, fakeId, timestamp),

            "instagram" when messageType == "comment" =>
                BuildCommentPayload("instagram", from, to, message, fakeId),

            "instagram" => BuildDmPayload("instagram", from, to, message, fakeId, timestamp),

            "facebook" when messageType == "comment" =>
                BuildCommentPayload("page", from, to, message, fakeId),

            "facebook" => BuildDmPayload("page", from, to, message, fakeId, timestamp),

            _ => "{}"
        };
    }

    private static string BuildWhatsAppPayload(
        string from, string to, string message, string fakeId, long timestamp) =>
        JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry   = new[]
            {
                new
                {
                    id      = "waba_test",
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                messaging_product = "whatsapp",
                                metadata  = new { display_phone_number = to, phone_number_id = to },
                                contacts  = new[] { new { profile = new { name = "Test User" }, wa_id = from } },
                                messages  = new[]
                                {
                                    new
                                    {
                                        from,
                                        id        = $"wamid.{fakeId}",
                                        timestamp,
                                        text      = new { body = message },
                                        type      = "text"
                                    }
                                }
                            },
                            field = "messages"
                        }
                    }
                }
            }
        });

    private static string BuildDmPayload(
        string objectType, string from, string to, string message, string fakeId, long timestamp) =>
        JsonSerializer.Serialize(new
        {
            @object = objectType,
            entry   = new[]
            {
                new
                {
                    id        = to,
                    messaging = new[]
                    {
                        new
                        {
                            sender    = new { id = from },
                            recipient = new { id = to },
                            timestamp,
                            message   = new { mid = $"mid.{fakeId}", text = message }
                        }
                    }
                }
            }
        });

    private static string BuildCommentPayload(
        string objectType, string from, string to, string message, string fakeId)
    {
        var field    = objectType == "instagram" ? "comments" : "feed";
        var valueObj = objectType == "instagram"
            ? (object)new { comment_id = fakeId, text    = message, from = new { id = from }, media_id = to }
            : (object)new { comment_id = fakeId, message,            from = new { id = from }, post_id  = to };

        return JsonSerializer.Serialize(new
        {
            @object = objectType,
            entry   = new[]
            {
                new
                {
                    id      = to,
                    changes = new[] { new { value = valueObj, field } }
                }
            }
        });
    }
}


/// <summary>
/// Test webhook request. Tenant is NOT supplied — it is resolved from the asset IDs
/// (To field) exactly as in production. The To field must match an ExternalId of an
/// active MetaChannelAsset in the database for the target tenant.
/// </summary>
public sealed record WebhookTestRequest(
    string Platform,
    string From,
    string To,
    string Message,
    string? MessageType    = "text",
    string? RawPayloadJson = null,
    bool   UseRawPayload   = false);

public sealed record WebhookTestResponse(
    string       RequestPayload,
    int          StatusCode,
    bool         Success,
    string       ResultMessage,
    int          ProcessedCount,
    string?      DetectedIntent,
    string?      ReplySent,
    string?      ErrorMessage,
    List<string> ToolCallsMade,
    int          InputTokens,
    int          OutputTokens,
    string       AiProvider,
    string       AiModel);
