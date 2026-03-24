using System;
using System.Collections.Generic;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

// ─── Query ───────────────────────────────────────────────────────────────────

public record GetProductsQuery(
    Guid TenantId,
    int PageIndex = 0,
    int PageSize = 20,
    string? Search = null) : IRequest<ProductPageResult>;

// ─── Results ─────────────────────────────────────────────────────────────────

public record ProductPageResult(IReadOnlyList<ProductSummaryDto> Items, int TotalCount);

public record ProductSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    decimal BasePrice,
    string Currency,
    int TotalStock,
    string? Sku,
    int VariantCount,
    string? PrimaryImageUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProductDetailDto(
    Guid Id,
    string Name,
    string? Description,
    decimal BasePrice,
    string Currency,
    int TotalStock,
    string? Sku,
    string? ExternalId,
    List<ProductVariantDetailDto> Variants,
    List<ProductImageDetailDto> Images,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProductVariantDetailDto(Guid Id, string Size, string? Color, int Stock, decimal? PriceOverride);

public record ProductImageDetailDto(Guid Id, string Url, string? AltText, bool IsPrimary);
