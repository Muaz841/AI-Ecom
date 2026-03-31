using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

public sealed record PosePrescriptSummary(
    Guid     Id,
    string   Name,
    string   ReferenceImagePath,
    DateTime CreatedAt);

public interface IPosePrescriptRepository
{
    Task<PosePrescript?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PosePrescriptSummary>> GetActiveByTenantAsync(CancellationToken ct = default);

    Task AddAsync(PosePrescript pose, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
