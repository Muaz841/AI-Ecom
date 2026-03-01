using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Business.Commands;

public class ProductUploadCommandHandler : IRequestHandler<ProductUploadCommand, ProductUploadResult>
{
    private readonly IRepository<Product> _productRepository;
    private readonly ILogger<ProductUploadCommandHandler> _logger;

    public ProductUploadCommandHandler(
        IRepository<Product> productRepository,
        ILogger<ProductUploadCommandHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<ProductUploadResult> Handle(ProductUploadCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = Product.Create(
                request.ClientId,
                request.Name,
                request.BasePrice,
                request.Description,
                request.Currency,
                request.Sku);

            if (request.Variants is not null)
            {
                foreach (var variant in request.Variants)
                {
                    product.AddVariant(variant.Size, variant.Color, variant.Stock, variant.PriceOverride);
                }
            }

            if (request.ImageUrls is not null)
            {
                for (var i = 0; i < request.ImageUrls.Count; i++)
                {
                    product.AddImage(request.ImageUrls[i], isPrimary: i == 0);
                }
            }

            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();

            _logger.LogInformation("Product {ProductId} uploaded for client {ClientId}", product.Id, request.ClientId);
            return new ProductUploadResult(true, product.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload product for client {ClientId}", request.ClientId);
            return new ProductUploadResult(false, null, ex.Message);
        }
    }
}
