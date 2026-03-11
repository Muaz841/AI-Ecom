using System.Security.Claims;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/integrations/meta")]
public class IntegrationsController : ControllerBase
{
    private readonly IMetaIntegrationService _metaIntegrationService;

    public IntegrationsController(IMetaIntegrationService metaIntegrationService)
    {
        _metaIntegrationService = metaIntegrationService;
    }

    [Authorize(Policy = PermissionCodes.IntegrationsManage)]
    [HttpPost("{channel}/start")]
    public async Task<IActionResult> Start(string channel, [FromBody] MetaStartConnectionRequest? request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var userId = ResolveUserId();
        if (!tenantId.HasValue || !userId.HasValue)
        {
            return Unauthorized("Missing tenant/user claims.");
        }

        var result = await _metaIntegrationService.StartConnectionAsync(
            tenantId.Value,
            userId.Value,
            channel,
            request?.ReturnUrl,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string channel,
        [FromQuery] string state,
        [FromQuery] string code,
        [FromQuery] string? returnUrl,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect(BuildUiRedirect(returnUrl, false, $"meta_error:{error}:{error_description}"));
        }

        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("channel, state and code are required.");
        }

        var result = await _metaIntegrationService.CompleteConnectionAsync(channel, state, code, returnUrl, cancellationToken);
        if (!result.Success)
        {
            return Redirect(BuildUiRedirect(returnUrl, false, result.ErrorMessage));
        }

        return Redirect(BuildUiRedirect(returnUrl, true, null));
    }

    [Authorize(Policy = PermissionCodes.IntegrationsRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        if (!tenantId.HasValue)
        {
            return Unauthorized("Missing tenant claims.");
        }

        var rows = await _metaIntegrationService.ListConnectionsAsync(tenantId.Value, cancellationToken);
        return Ok(rows);
    }

    [Authorize(Policy = PermissionCodes.IntegrationsManage)]
    [HttpDelete("{connectionId:guid}")]
    public async Task<IActionResult> Disconnect(Guid connectionId, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        if (!tenantId.HasValue)
        {
            return Unauthorized("Missing tenant claims.");
        }

        var deleted = await _metaIntegrationService.DisconnectAsync(tenantId.Value, connectionId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private Guid? ResolveTenantId()
    {
        var raw = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("client_id");
        return Guid.TryParse(raw, out var tenantId) ? tenantId : null;
    }

    private Guid? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string BuildUiRedirect(string? returnUrl, bool success, string? errorMessage)
    {
        var target = string.IsNullOrWhiteSpace(returnUrl)
            ? "/settings/integrations"
            : returnUrl;

        if (success)
        {
            return $"{target}?metaConnect=success";
        }

        var encoded = Uri.EscapeDataString(errorMessage ?? "meta_connect_failed");
        return $"{target}?metaConnect=error&reason={encoded}";
    }
}

public sealed record MetaStartConnectionRequest(string? ReturnUrl);

