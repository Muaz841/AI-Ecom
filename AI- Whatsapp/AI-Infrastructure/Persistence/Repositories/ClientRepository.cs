using System;
using System.Linq;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public class ClientRepository : EfRepository<Client>
{
    public ClientRepository(PlatformDbContext context) : base(context)
    {
    }

    public async Task<Client?> GetByMetaIdentifiersAsync(
        string? metaPageId = null,
        string? whatsAppBusinessAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(metaPageId) && string.IsNullOrWhiteSpace(whatsAppBusinessAccountId))
        {
            return null;
        }

        IQueryable<Client> query = _dbSet;

        if (!string.IsNullOrWhiteSpace(metaPageId))
        {
            query = query.Where(c => c.MetaPageId == metaPageId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(whatsAppBusinessAccountId))
        {
            query = query.Where(c => c.WhatsAppBusinessAccountId == whatsAppBusinessAccountId.Trim());
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<Client?> GetByExternalStoreIdAsync(string externalStoreId)
    {
        if (string.IsNullOrWhiteSpace(externalStoreId))
        {
            return null;
        }

        var trimmed = externalStoreId.Trim();

        return await _dbSet.FirstOrDefaultAsync(c =>
            c.ShopifyStoreId == trimmed ||
            c.WooCommerceStoreId == trimmed);
    }
}
