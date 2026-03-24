using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public class AddProductImageCommandHandler : IRequestHandler<AddProductImageCommand, ProductMutationResult>
{
    private readonly IProductRepository _products;
    private readonly IApplicationLogger _logger;

    public AddProductImageCommandHandler(IProductRepository products, IApplicationLogger logger)
    {
        _products = products;
        _logger   = logger;
    }

    public async Task<ProductMutationResult> Handle(AddProductImageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.GetByIdAsync(request.TenantId, request.ProductId, cancellationToken);
            if (product is null)
                return new ProductMutationResult(false, null, "Product not found.");

            if (product.Images.Count >= 20)
                return new ProductMutationResult(false, null, "Maximum of 20 images per product.");

            product.AddImage(request.Url, request.AltText, request.IsPrimary);
            await _products.UpdateAsync(product, cancellationToken);

            _logger.Info("Image added to product {ProductId}", request.ProductId);
            return new ProductMutationResult(true, request.ProductId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add image to product {ProductId}", request.ProductId);
            return new ProductMutationResult(false, null, ex.Message);
        }
    }
}

public class DeleteProductImageCommandHandler : IRequestHandler<DeleteProductImageCommand, ProductMutationResult>
{
    private readonly IProductRepository _products;
    private readonly IApplicationLogger _logger;

    public DeleteProductImageCommandHandler(IProductRepository products, IApplicationLogger logger)
    {
        _products = products;
        _logger   = logger;
    }

    public async Task<ProductMutationResult> Handle(DeleteProductImageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.GetByIdAsync(request.TenantId, request.ProductId, cancellationToken);
            if (product is null)
                return new ProductMutationResult(false, null, "Product not found.");

            if (!product.RemoveImage(request.ImageId))
                return new ProductMutationResult(false, null, "Image not found.");

            await _products.UpdateAsync(product, cancellationToken);

            _logger.Info("Image {ImageId} removed from product {ProductId}", request.ImageId, request.ProductId);
            return new ProductMutationResult(true, request.ProductId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to remove image {ImageId} from product {ProductId}", request.ImageId, request.ProductId);
            return new ProductMutationResult(false, null, ex.Message);
        }
    }
}

public class SetPrimaryImageCommandHandler : IRequestHandler<SetPrimaryImageCommand, ProductMutationResult>
{
    private readonly IProductRepository _products;
    private readonly IApplicationLogger _logger;

    public SetPrimaryImageCommandHandler(IProductRepository products, IApplicationLogger logger)
    {
        _products = products;
        _logger   = logger;
    }

    public async Task<ProductMutationResult> Handle(SetPrimaryImageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.GetByIdAsync(request.TenantId, request.ProductId, cancellationToken);
            if (product is null)
                return new ProductMutationResult(false, null, "Product not found.");

            if (!product.MarkImageAsPrimary(request.ImageId))
                return new ProductMutationResult(false, null, "Image not found.");

            await _products.UpdateAsync(product, cancellationToken);
            return new ProductMutationResult(true, request.ProductId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set primary image for product {ProductId}", request.ProductId);
            return new ProductMutationResult(false, null, ex.Message);
        }
    }
}
