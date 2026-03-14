using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlatformMetaConfigRepository : IPlatformMetaConfigRepository
{
    private readonly PlatformDbContext _db;

    public PlatformMetaConfigRepository(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<PlatformMetaConfig?> GetAsync(CancellationToken cancellationToken = default)
        => _db.PlatformMetaConfigs
              .AsNoTracking()
              .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(PlatformMetaConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PlatformMetaConfigs.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            await _db.PlatformMetaConfigs.AddAsync(config, cancellationToken);
        }
        else
        {
            _db.PlatformMetaConfigs.Update(config);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
