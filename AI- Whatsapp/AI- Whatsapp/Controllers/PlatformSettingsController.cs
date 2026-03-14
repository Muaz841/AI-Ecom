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
[Route("api/host/platform")]
[Authorize(Policy = PermissionCodes.PlatformSettings)]
public class PlatformSettingsController : ControllerBase
{
    private readonly IPlatformSettingsService _service;

    public PlatformSettingsController(IPlatformSettingsService service)
    {
        _service = service;
    }

    [HttpGet("meta")]
    [SwaggerOperation(Summary = "Get Meta app configuration", Description = "Returns current Meta OAuth app credentials. AppSecret is always masked.")]
    [ProducesResponseType(typeof(PlatformMetaConfigResult), 200)]
    public async Task<ActionResult<PlatformMetaConfigResult>> GetMetaConfig(CancellationToken cancellationToken)
    {
        var result = await _service.GetMetaConfigAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("meta")]
    [SwaggerOperation(Summary = "Save Meta app configuration", Description = "Creates or updates the platform-level Meta OAuth credentials. Pass an empty AppSecret to keep the existing secret unchanged.")]
    [ProducesResponseType(typeof(PlatformMetaConfigResult), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<PlatformMetaConfigResult>> SaveMetaConfig(
        [FromBody] SaveMetaConfigApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SaveMetaConfigAsync(
                new SavePlatformMetaConfigRequest(
                    request.AppId,
                    request.AppSecret,
                    request.LoginConfigurationId,
                    request.GraphVersion,
                    request.CallbackBaseUrl),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = "Validation failed", detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { title = "Operation failed", detail = ex.Message });
        }
    }
}

public sealed record SaveMetaConfigApiRequest(
    [Required] string AppId,
    /// <summary>Null or empty to keep the existing secret. Pass a masked placeholder (•••) to keep it too.</summary>
    string? AppSecret,
    /// <summary>Optional Facebook Login for Business configuration ID.</summary>
    string? LoginConfigurationId,
    [Required] string GraphVersion,
    [Required] string CallbackBaseUrl);
