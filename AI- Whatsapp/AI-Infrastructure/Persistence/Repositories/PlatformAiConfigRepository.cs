using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class PlatformAiConfigRepository : IPlatformAiConfigRepository
{
    private readonly PlatformDbContext _db;

    public PlatformAiConfigRepository(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<PlatformAiConfig?> GetAsync(CancellationToken cancellationToken = default)
        => _db.PlatformAiConfigs
              .AsNoTracking()
              .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(PlatformAiConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PlatformAiConfigs.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            await _db.PlatformAiConfigs.AddAsync(config, cancellationToken);
        }
        else
        {
            // existing is tracked; copy scalar values from the updated (untracked) entity onto it.
            _db.Entry(existing).CurrentValues.SetValues(config);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
