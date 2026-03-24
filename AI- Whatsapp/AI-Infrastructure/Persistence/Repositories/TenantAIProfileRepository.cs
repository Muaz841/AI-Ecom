using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantAIProfileRepository : ITenantAIProfileRepository
{
    private readonly PlatformDbContext _db;

    public TenantAIProfileRepository(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<TenantAIProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
        => _db.TenantAIProfiles
              .AsNoTracking()
              .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);

    public async Task SaveAsync(TenantAIProfile profile, CancellationToken ct = default)
    {
        var existing = await _db.TenantAIProfiles
            .FirstOrDefaultAsync(p => p.TenantId == profile.TenantId, ct);

        if (existing is null)
            await _db.TenantAIProfiles.AddAsync(profile, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(profile);

        await _db.SaveChangesAsync(ct);
    }
}
