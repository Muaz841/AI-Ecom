using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionCodes.ProductsManage)]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
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
    [SwaggerOperation(Summary = "Create product", Description = "Creates a product with variants and image URLs.")]
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
            request.Variants,
            request.ImageUrls);

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

    [HttpPost("{id:guid}/images")]
    [SwaggerOperation(Summary = "Add image", Description = "Adds a URL-based image to a product.")]
    [ProducesResponseType(typeof(ProductMutationResult), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ProductMutationResult>> AddImage(
        Guid id,
        [FromBody] AddImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var command = new AddProductImageCommand(tenantId, id, request.Url, request.AltText, request.IsPrimary);
        var result  = await _mediator.Send(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result.ErrorMessage);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [SwaggerOperation(Summary = "Remove image", Description = "Removes an image from a product.")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(new DeleteProductImageCommand(tenantId, id, imageId), cancellationToken);
        if (!result.Success && result.ErrorMessage?.Contains("not found") == true)
            return NotFound();

        return result.Success ? NoContent() : BadRequest(result.ErrorMessage);
    }

    [HttpPut("{id:guid}/images/{imageId:guid}/primary")]
    [SwaggerOperation(Summary = "Set primary image", Description = "Marks an image as the primary product image.")]
    [ProducesResponseType(typeof(ProductMutationResult), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProductMutationResult>> SetPrimaryImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();

        var result = await _mediator.Send(new SetPrimaryImageCommand(tenantId, id, imageId), cancellationToken);
        if (!result.Success && result.ErrorMessage?.Contains("not found") == true)
            return NotFound();

        return result.Success ? Ok(result) : BadRequest(result.ErrorMessage);
    }

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
    System.Collections.Generic.List<ProductVariantDto>? Variants,
    System.Collections.Generic.List<string>? ImageUrls);

public record UpdateProductRequest(
    string Name,
    string? Description,
    decimal BasePrice,
    string? Currency,
    string? Sku,
    System.Collections.Generic.List<ProductVariantUpdateDto>? Variants);

public record AddImageRequest(string Url, string? AltText = null, bool IsPrimary = false);
