using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, ProductMutationResult>
{
    private readonly IProductRepository _products;
    private readonly IApplicationLogger _logger;

    public DeleteProductCommandHandler(IProductRepository products, IApplicationLogger logger)
    {
        _products = products;
        _logger   = logger;
    }

    public async Task<ProductMutationResult> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.GetByIdAsync(request.TenantId, request.ProductId, cancellationToken);
            if (product is null)
                return new ProductMutationResult(false, null, "Product not found.");

            await _products.DeleteAsync(request.ProductId, cancellationToken);

            _logger.Info("Product {ProductId} deleted for tenant {TenantId}", request.ProductId, request.TenantId);
            return new ProductMutationResult(true, request.ProductId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete product {ProductId}", request.ProductId);
            return new ProductMutationResult(false, null, ex.Message);
        }
    }
}
