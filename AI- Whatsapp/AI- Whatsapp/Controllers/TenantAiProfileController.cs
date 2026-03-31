using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/v1/tenant/ai-profile")]
[Authorize(Policy = PermissionCodes.AiManage)]
public class TenantAiProfileController : ControllerBase
{
    private readonly ITenantAIProfileService _service;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public TenantAiProfileController(
        ITenantAIProfileService service,
        ICurrentTenantAccessor tenantAccessor)
    {
        _service = service;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Returns the AI persona profile for the current tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result = await _service.GetProfileAsync(tenantId, ct);
        return result is null ? NoContent() : Ok(result);
    }

    /// <summary>Creates or updates the AI persona profile for the current tenant.</summary>
    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody] SaveAiProfileApiRequest request,
        CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var result = await _service.SaveProfileAsync(
            tenantId,
            new SaveTenantAIProfileRequest(
                request.SystemPrompt,
                request.Tone,
                request.Language,
                request.BrandRules,
                request.ForbiddenTopics,
                request.DefaultResponseStyle,
                request.AiCallsPerHourLimit,
                request.PoseExtractionPrompt,
                request.ImageGenerationPrompt),
            ct);

        return Ok(result);
    }

    private Guid GetTenantId()
    {
        var tenantId = _tenantAccessor.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException("Tenant context is required for this operation.");
        return tenantId.Value;
    }
}

public sealed record SaveAiProfileApiRequest(
    [Required][MinLength(10)] string SystemPrompt,
    string? Tone = null,
    string? Language = null,
    string? BrandRules = null,
    string? ForbiddenTopics = null,
    string? DefaultResponseStyle = null,
    [Range(0, 10000)] int AiCallsPerHourLimit = 200,
    string? PoseExtractionPrompt = null,
    string? ImageGenerationPrompt = null);
