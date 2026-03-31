using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class PosePrescriptRepository : IPosePrescriptRepository
{
    private readonly PlatformDbContext _db;

    public PosePrescriptRepository(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<PosePrescript?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.PosePrescripts
              .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);

    public async Task<IReadOnlyList<PosePrescriptSummary>> GetActiveByTenantAsync(CancellationToken ct = default)
    {
        var results = await _db.PosePrescripts
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PosePrescriptSummary(p.Id, p.Name, p.ReferenceImagePath, p.CreatedAt))
            .ToListAsync(ct);

        return results;
    }

    public async Task AddAsync(PosePrescript pose, CancellationToken ct = default)
    {
        await _db.PosePrescripts.AddAsync(pose, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
