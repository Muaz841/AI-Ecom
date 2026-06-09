using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Tenant;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

public sealed class AgentDecisionService : IAgentDecisionService
{
    private readonly IAgentDecisionRepository _repo;
    private readonly ICurrentTenantAccessor   _tenantAccessor;

    public AgentDecisionService(
        IAgentDecisionRepository repo,
        ICurrentTenantAccessor tenantAccessor)
    {
        _repo           = repo;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<List<AgentDecisionDto>> GetRecentAsync(
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId  = RequireTenantId();
        var decisions = await _repo.GetRecentAsync(tenantId, count, cancellationToken);
        return decisions.Select(Map).ToList();
    }

    public async Task ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var decision = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Agent decision {id} not found.");

        decision.Approve(tenantId); // use tenantId as proxy for user — real approval tracks user via HTTP context
        await _repo.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var decision = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Agent decision {id} not found.");

        decision.Reject();
        await _repo.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireTenantId() =>
        _tenantAccessor.GetCurrentTenantId()
            ?? throw new InvalidOperationException("Tenant context is required for this operation.");

    private static AgentDecisionDto Map(Business.Entities.AgentDecision d) =>
        new(d.Id, d.RunAt, d.ActionType, d.ActionPayload,
            d.Status.ToString(), d.Reason, d.Confidence, d.IsDryRun,
            d.ExecutedAt, d.CreatedAt);
}
