using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionCodes.ProductsManage)]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;

    public ProductsController(IMediator mediator, IFileStorageService fileStorage)
    {
        _mediator    = mediator;
        _fileStorage = fileStorage;
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    [SwaggerOperation(Summary = "List products", Description = "Returns paginated products for the tenant.")]
    [ProducesResponseType(typeof(ProductPageResult), 200)]
    public async Task<ActionResult<ProductPageResult>> List(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize  = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(
            new GetProductsQuery(tenantId, pageIndex, pageSize, search),
            cancellationToken);

        return Ok(result);
    }

    // ─── Get by ID ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get product", Description = "Returns full product detail including variants and images.")]
    [ProducesResponseType(typeof(ProductDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProductDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(new GetProductByIdQuery(tenantId, id), cancellationToken);
        if (result is null) return NotFound();

        return Ok(result);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    [HttpPost("upload")]
    [SwaggerOperation(Summary = "Create product", Description = "Creates a product with variants.")]
    [ProducesResponseType(typeof(ProductUploadResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Upload(
        [FromBody] ProductUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var command = new ProductUploadCommand(
            tenantId,
            request.Name,
            request.Description,
            request.BasePrice,
            request.Currency ?? "PKR",
            request.Sku,
            request.Variants);

        var result = await _mediator.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result.ErrorMessage);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    [SwaggerOperation(Summary = "Update product", Description = "Updates product details and variant stock levels.")]
    [ProducesResponseType(typeof(ProductMutationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProductMutationResult>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var command = new UpdateProductCommand(
            tenantId,
            id,
            request.Name,
            request.Description,
            request.BasePrice,
            request.Currency ?? "PKR",
            request.Sku,
            request.Variants);

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.Success && result.ErrorMessage == "Product not found.")
            return NotFound();

        return result.Success ? Ok(result) : BadRequest(result.ErrorMessage);
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Summary = "Delete product", Description = "Permanently deletes a product and all its variants and images.")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(new DeleteProductCommand(tenantId, id), cancellationToken);
        if (!result.Success && result.ErrorMessage == "Product not found.")
            return NotFound();

        return result.Success ? NoContent() : BadRequest(result.ErrorMessage);
    }

    // ─── Images ───────────────────────────────────────────────────────────────

    

   

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

// ─── Request DTOs (controller-layer only) ────────────────────────────────────

public record ProductUploadRequest(
    string Name,
    string? Description,
    decimal BasePrice,
    string? Currency,
    string? Sku,
    System.Collections.Generic.List<ProductVariantDto>? Variants);

public record UpdateProductRequest(
    string Name,
    string? Description,
    decimal BasePrice,
    string? Currency,
    string? Sku,
    System.Collections.Generic.List<ProductVariantUpdateDto>? Variants);
