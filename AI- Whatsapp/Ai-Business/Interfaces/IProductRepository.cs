using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid clientId, Guid productId);

    Task<IReadOnlyList<Product>> GetAvailableProductsAsync(
        Guid clientId,
        int? maxItems = 20,
        string? searchTerm = null);

    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        Guid clientId,
        int threshold = 5);

    Task AddAsync(Product product);

    Task UpdateAsync(Product product);

    Task DeleteAsync(Guid productId);

    Task<bool> ExistsAsync(Guid clientId, string skuOrName);
}
