using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/host/ai-settings")]
[Authorize(Policy = PermissionCodes.PlatformSettings)]
public class AiSettingsController : ControllerBase
{
    private readonly IPlatformAiSettingsService _service;

    public AiSettingsController(IPlatformAiSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get AI provider configuration",
        Description = "Returns current AI provider settings. API keys are always masked in the response.")]
    [ProducesResponseType(typeof(PlatformAiConfigResult), 200)]
    public async Task<ActionResult<PlatformAiConfigResult>> GetAiConfig(CancellationToken cancellationToken)
    {
        var result = await _service.GetAiConfigAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [SwaggerOperation(
        Summary = "Save AI provider configuration",
        Description = "Creates or updates host-level AI provider settings. Pass a masked placeholder or null for API keys to keep existing values unchanged.")]
    [ProducesResponseType(typeof(PlatformAiConfigResult), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<PlatformAiConfigResult>> SaveAiConfig(
        [FromBody] SaveAiConfigApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SaveAiConfigAsync(
                new SavePlatformAiConfigRequest(
                    ActiveProvider: request.ActiveProvider,
                    DebugModeEnabled: request.DebugModeEnabled,
                    OllamaEndpoint: request.OllamaEndpoint,
                    OllamaModel: request.OllamaModel,
                    OpenAIModel: request.OpenAIModel,
                    OpenAIApiKey: request.OpenAIApiKey,
                    GeminiModel: request.GeminiModel,
                    GeminiApiKey: request.GeminiApiKey,
                    RequestTimeoutSeconds: request.RequestTimeoutSeconds,
                    EnableToolCalling: request.EnableToolCalling,
                    EnableStructuredOutput: request.EnableStructuredOutput,
                    Temperature: request.Temperature,
                    TopP: request.TopP,
                    MaxTokens: request.MaxTokens),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = "Validation failed", detail = ex.Message });
        }
    }

    [HttpGet("models")]
    [SwaggerOperation(
        Summary = "Get available models for a provider",
        Description = "Returns the list of AI models available for the given provider. Results are cached for 3 hours. Use refresh=true to force a fresh fetch.")]
    [ProducesResponseType(typeof(AiModelListResult), 200)]
    public async Task<ActionResult<AiModelListResult>> GetModels(
        [FromQuery, Required] string provider,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetModelsAsync(provider, refresh, cancellationToken);
        return Ok(result);
    }
}
public sealed record SaveAiConfigApiRequest(
    [Required] string ActiveProvider,
    bool DebugModeEnabled,
    string? OllamaEndpoint,
    string? OllamaModel,
    string? OpenAIModel,
    string? OpenAIApiKey,
    string? GeminiModel,
    string? GeminiApiKey,
    [Range(10, 300)] int RequestTimeoutSeconds = 60,
    bool EnableToolCalling = false,
    bool EnableStructuredOutput = false,
    double? Temperature = null,
    double? TopP = null,
    int? MaxTokens = null);
