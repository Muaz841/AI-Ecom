using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/host/tenants")]
[Authorize(Policy = PermissionCodes.TenantsManage)]
public class TenantsController : ControllerBase
{
    private readonly ITenantProvisioningService _provisioningService;

    public TenantsController(ITenantProvisioningService provisioningService)
    {
        _provisioningService = provisioningService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenants = await _provisioningService.ListTenantsAsync(cancellationToken);
        return Ok(tenants);
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _provisioningService.GetTenantAsync(tenantId, cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantApiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _provisioningService.CreateTenantAsync(
                new CreateTenantRequest(
                    request.Name,
                    request.BusinessName,
                    request.AdminEmail,
                    request.AdminPassword,
                    request.AdminFirstName,
                    request.AdminLastName),
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return CreatedAtAction(nameof(Get), new { tenantId = result.TenantId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{tenantId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _provisioningService.SuspendTenantAsync(tenantId, cancellationToken);
            return success ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{tenantId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid tenantId, CancellationToken cancellationToken)
    {
        var success = await _provisioningService.ActivateTenantAsync(tenantId, cancellationToken);
        return success ? NoContent() : NotFound();
    }
}

public sealed record CreateTenantApiRequest(
    string Name,
    string BusinessName,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName);
