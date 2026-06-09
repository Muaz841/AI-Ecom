using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class AgentDecisionRepository : IAgentDecisionRepository
{
    private readonly PlatformDbContext _db;

    public AgentDecisionRepository(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<List<AgentDecision>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken cancellationToken = default)
        => _db.AgentDecisions
              .AsNoTracking()
              .Where(d => d.TenantId == tenantId)
              .OrderByDescending(d => d.RunAt)
              .Take(count)
              .ToListAsync(cancellationToken);

    public Task<AgentDecision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.AgentDecisions
              .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<List<AgentDecisionVector>> GetWithEmbeddingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _db.AgentDecisions
              .AsNoTracking()
              .Where(d => d.TenantId == tenantId && d.EmbeddingJson != null && d.OutcomeLabel != null)
              .Select(d => new AgentDecisionVector(
                  d.Id, d.ActionType, d.Reason, d.Confidence,
                  d.OutcomeLabel, d.RunAt, d.EmbeddingJson!))
              .ToListAsync(cancellationToken);

    public Task<List<AgentDecision>> GetPendingEmbeddingAsync(Guid tenantId, int maxRows = 50, CancellationToken cancellationToken = default)
        => _db.AgentDecisions
              .Where(d => d.TenantId == tenantId && d.EmbeddingJson == null)
              .OrderBy(d => d.CreatedAt)
              .Take(maxRows)
              .ToListAsync(cancellationToken);

    public Task<int> CountWithOutcomesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _db.AgentDecisions
              .AsNoTracking()
              .CountAsync(d => d.TenantId == tenantId && d.OutcomeLabel != null, cancellationToken);

    public async Task AddAsync(AgentDecision decision, CancellationToken cancellationToken = default)
        => await _db.AgentDecisions.AddAsync(decision, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
