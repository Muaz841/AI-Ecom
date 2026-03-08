using System;
using System.Collections.Generic;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record ProductUploadCommand(
    Guid TenantId,
    string Name,
    string? Description,
    decimal BasePrice,
    string Currency = "PKR",
    string? Sku = null,
    List<ProductVariantDto>? Variants = null,
    List<string>? ImageUrls = null) : IRequest<ProductUploadResult>;

public record ProductVariantDto(
    string Size,
    string? Color,
    int Stock,
    decimal? PriceOverride = null);

public record ProductUploadResult(
    bool Success,
    Guid? ProductId,
    string? ErrorMessage = null);

