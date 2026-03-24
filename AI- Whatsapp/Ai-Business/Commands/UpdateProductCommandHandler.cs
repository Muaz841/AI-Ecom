using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, ProductMutationResult>
{
    private readonly IProductRepository _products;
    private readonly IApplicationLogger _logger;

    public UpdateProductCommandHandler(IProductRepository products, IApplicationLogger logger)
    {
        _products = products;
        _logger   = logger;
    }

    public async Task<ProductMutationResult> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.GetByIdAsync(request.TenantId, request.ProductId, cancellationToken);
            if (product is null)
                return new ProductMutationResult(false, null, "Product not found.");

            product.Update(request.Name, request.Description, request.BasePrice, request.Currency, request.Sku);

            if (request.Variants is not null)
            {
                // Remove variants no longer in the payload
                var incomingIds = request.Variants
                    .Where(v => v.Id.HasValue)
                    .Select(v => v.Id!.Value)
                    .ToHashSet();

                foreach (var existing in product.Variants.ToList())
                {
                    if (!incomingIds.Contains(existing.Id))
                        product.RemoveVariant(existing.Id);
                }

                // Update existing / add new
                foreach (var dto in request.Variants)
                {
                    if (dto.Id.HasValue)
                        product.SetVariantStock(dto.Id.Value, dto.Stock);
                    else
                        product.AddVariant(dto.Size, dto.Color, dto.Stock, dto.PriceOverride);
                }
            }

            await _products.UpdateAsync(product, cancellationToken);

            _logger.Info("Product {ProductId} updated for tenant {TenantId}", product.Id, request.TenantId);
            return new ProductMutationResult(true, product.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update product {ProductId}", request.ProductId);
            return new ProductMutationResult(false, null, ex.Message);
        }
    }
}
