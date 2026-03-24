using System;
using System.Collections.Generic;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record UpdateProductCommand(
    Guid TenantId,
    Guid ProductId,
    string Name,
    string? Description,
    decimal BasePrice,
    string Currency,
    string? Sku,
    List<ProductVariantUpdateDto>? Variants = null) : IRequest<ProductMutationResult>;

public record ProductVariantUpdateDto(
    Guid? Id,      
    string Size,
    string? Color,
    int Stock,
    decimal? PriceOverride = null);

public record ProductMutationResult(bool Success, Guid? ProductId = null, string? ErrorMessage = null);
