using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductInventoryItem>> GetAvailableInventoryAsync(
        Guid TenantId,
        int? maxItems = 20,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    Task<Product?> GetByIdAsync(Guid TenantId, Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAvailableProductsAsync(
        Guid TenantId,
        int? maxItems = 20,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        Guid TenantId,
        int threshold = 5,
        CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid TenantId, string skuOrName, CancellationToken cancellationToken = default);
}

public sealed record ProductInventoryItem(
    Guid ProductId,
    string Name,
    decimal BasePrice,
    string Currency,
    int TotalStock,
    string? Sku);

