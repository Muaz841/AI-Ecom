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
[Route("api/host/marketing")]
[Authorize(Policy = PermissionCodes.PlatformSettings)]
public class MarketingSettingsController : ControllerBase
{
    private readonly IPlatformMarketingSettingsService _service;

    public MarketingSettingsController(IPlatformMarketingSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get Marketing Engine configuration",
        Description = "Returns current Claude and Meta Ads settings. API keys are always masked in the response.")]
    [ProducesResponseType(typeof(PlatformMarketingConfigResult), 200)]
    public async Task<ActionResult<PlatformMarketingConfigResult>> GetConfig(CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [SwaggerOperation(
        Summary = "Save Marketing Engine configuration",
        Description = "Creates or updates host-level Marketing Engine settings. Pass a masked placeholder or null for API keys to keep existing values unchanged.")]
    [ProducesResponseType(typeof(PlatformMarketingConfigResult), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<PlatformMarketingConfigResult>> SaveConfig(
        [FromBody] SaveMarketingConfigApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SaveAsync(
                new SavePlatformMarketingConfigRequest(
                    ClaudeApiKey:        request.ClaudeApiKey,
                    ClaudeDecisionModel: request.ClaudeDecisionModel ?? "claude-opus-4-6",
                    ClaudeSummaryModel:  request.ClaudeSummaryModel  ?? "claude-haiku-4-5-20251001",
                    MetaAdsAccountId:    request.MetaAdsAccountId,
                    MetaAdsAccessToken:  request.MetaAdsAccessToken,
                    DryRun:              request.DryRun,
                    MaxActionsPerDay:    request.MaxActionsPerDay,
                    DailySpendCapUsd:    request.DailySpendCapUsd),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = "Validation failed", detail = ex.Message });
        }
    }
}

public sealed record SaveMarketingConfigApiRequest(
    string? ClaudeApiKey,
    string? ClaudeDecisionModel,
    string? ClaudeSummaryModel,
    string? MetaAdsAccountId,
    string? MetaAdsAccessToken,
    bool DryRun = true,
    [Range(1, 50)] int MaxActionsPerDay = 10,
    [Range(1, 100000)] decimal DailySpendCapUsd = 100m);
