using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ProductPageResult>
{
    private readonly IRepository<Product> _repo;

    public GetProductsQueryHandler(IRepository<Product> repo)
    {
        _repo = repo;
    }

    public async Task<ProductPageResult> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var search = request.Search?.Trim().ToLower();

        var total = await _repo.CountAsync(p =>
            string.IsNullOrEmpty(search) ||
            p.Name.ToLower().Contains(search) ||
            (p.Sku != null && p.Sku.ToLower().Contains(search)));

        var products = await _repo.ListAsync(
            predicate: p =>
                string.IsNullOrEmpty(search) ||
                p.Name.ToLower().Contains(search) ||
                (p.Sku != null && p.Sku.ToLower().Contains(search)),
            orderBy: q => q.OrderByDescending(p => p.CreatedAt),
            pageIndex: request.PageIndex,
            pageSize: request.PageSize,
            includes: new System.Linq.Expressions.Expression<Func<Product, object>>[]
            {
                p => p.Variants,
                p => p.Images
            });

        var items = products.Select(p => new ProductSummaryDto(
            Id:              p.Id,
            Name:            p.Name,
            Description:     p.Description,
            BasePrice:       p.BasePrice,
            Currency:        p.Currency,
            TotalStock:      p.TotalStock,
            Sku:             p.Sku,
            VariantCount:    p.Variants.Count,
            PrimaryImageUrl: p.Images.FirstOrDefault(i => i.IsPrimary)?.Url
                             ?? p.Images.FirstOrDefault()?.Url,
            CreatedAt:       p.CreatedAt,
            UpdatedAt:       p.UpdatedAt
        )).ToList();

        return new ProductPageResult(items, total);
    }
}

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IProductRepository _products;

    public GetProductByIdQueryHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var p = await _products.GetByIdAsync(request.TenantId, request.ProductId, cancellationToken);
        if (p is null) return null;

        return new ProductDetailDto(
            Id:          p.Id,
            Name:        p.Name,
            Description: p.Description,
            BasePrice:   p.BasePrice,
            Currency:    p.Currency,
            TotalStock:  p.TotalStock,
            Sku:         p.Sku,
            ExternalId:  p.ExternalId,
            Variants:    p.Variants.Select(v => new ProductVariantDetailDto(v.Id, v.Size, v.Color, v.Stock, v.PriceOverride)).ToList(),
            Images:      p.Images.Select(i => new ProductImageDetailDto(i.Id, i.Url, i.AltText, i.IsPrimary)).ToList(),
            CreatedAt:   p.CreatedAt,
            UpdatedAt:   p.UpdatedAt);
    }
}
