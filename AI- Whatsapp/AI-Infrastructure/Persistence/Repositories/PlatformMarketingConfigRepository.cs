using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlatformMarketingConfigRepository : IPlatformMarketingConfigRepository
{
    private readonly PlatformDbContext _db;

    public PlatformMarketingConfigRepository(PlatformDbContext db)
    {
        _db = db;
    }

    // IgnoreQueryFilters is required: PlatformMarketingConfig is a host-level singleton
    // with TenantId = null. The global EF tenant filter would otherwise exclude
    // this row when executing inside a tenant request context.
    public Task<PlatformMarketingConfig?> GetAsync(CancellationToken cancellationToken = default)
        => _db.PlatformMarketingConfigs
              .AsNoTracking()
              .IgnoreQueryFilters()
              .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(PlatformMarketingConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PlatformMarketingConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            await _db.PlatformMarketingConfigs.AddAsync(config, cancellationToken);
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(config);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
