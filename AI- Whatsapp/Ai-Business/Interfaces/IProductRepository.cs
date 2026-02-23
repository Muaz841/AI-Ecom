using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAvailableProductsAsync(
        Guid clientId,
        int? maxItems = 20,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        Guid clientId,
        int threshold = 5,
        CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid clientId, string skuOrName, CancellationToken cancellationToken = default);
}
