using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public class ProductRepository : EfRepository<Product>, IProductRepository
{
    public ProductRepository(PlatformDbContext context) : base(context)
    {
    }

    public async Task<Product?> GetByIdAsync(Guid clientId, Guid productId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId && p.ClientId == clientId, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAvailableProductsAsync(
        Guid clientId,
        int? maxItems = 20,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Where(p => p.ClientId == clientId && p.TotalStock > 0);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{searchTerm}%") ||
                EF.Functions.Like(p.Sku ?? string.Empty, $"%{searchTerm}%"));
        }

        IQueryable<Product> ordered = query
            .Include(p => p.Variants)
            .Include(p => p.Images.Where(i => i.IsPrimary))
            .OrderBy(p => p.Name);

        if (maxItems.HasValue && maxItems.Value > 0)
        {
            ordered = ordered.Take(maxItems.Value);
        }

        var results = await ordered.ToListAsync(cancellationToken);
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        Guid clientId,
        int threshold = 5,
        CancellationToken cancellationToken = default)
    {
        var results = await _dbSet
            .Where(p => p.ClientId == clientId && p.TotalStock <= threshold && p.TotalStock > 0)
            .Include(p => p.Variants)
            .OrderBy(p => p.TotalStock)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return results.AsReadOnly();
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(product);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid clientId, string skuOrName, CancellationToken cancellationToken = default)
    {
        var term = skuOrName.Trim();

        return await _dbSet.AnyAsync(
            p => p.ClientId == clientId &&
                 ((p.Sku != null && EF.Functions.Like(p.Sku, $"%{term}%")) ||
                  EF.Functions.Like(p.Name, $"%{term}%")),
            cancellationToken);
    }
}
