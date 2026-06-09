using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily marketing agent — runs at 09:00 PKT (04:00 UTC).
/// Iterates all active non-host tenants and runs the full agent pipeline for each.
/// </summary>
public sealed class MarketingAgentJob
{
    private readonly PlatformDbContext       _db;
    private readonly IMarketingAgentService  _agentService;
    private readonly ILogger<MarketingAgentJob> _logger;

    public MarketingAgentJob(
        PlatformDbContext db,
        IMarketingAgentService agentService,
        ILogger<MarketingAgentJob> logger)
    {
        _db           = db;
        _agentService = agentService;
        _logger       = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("MarketingAgentJob started at {UtcNow:u}", DateTime.UtcNow);

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Where(t => !t.IsHost && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync();

        _logger.LogInformation("MarketingAgentJob: {Count} active tenant(s) to process.", tenants.Count);

        foreach (var tenantId in tenants)
        {
            try
            {
                await _agentService.RunForTenantAsync(tenantId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketingAgentJob: unhandled error for tenant {TenantId}.", tenantId);
            }
        }

        _logger.LogInformation("MarketingAgentJob completed at {UtcNow:u}.", DateTime.UtcNow);
    }
}

/// <summary>
/// Syncs Meta campaign performance data every 6 hours.
/// Phase 1 stub — real Meta Ads API wiring in Phase 4.
/// </summary>
public sealed class MetaSyncJob
{
    private readonly ILogger<MetaSyncJob> _logger;

    public MetaSyncJob(ILogger<MetaSyncJob> logger)
    {
        _logger = logger;
    }

    public Task Execute()
    {
        _logger.LogInformation("MetaSyncJob: campaign sync triggered (Meta Ads API not yet wired).");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Hourly budget guard — enforces daily spend cap.
/// Phase 1 stub — real Meta live-spend data in Phase 4.
/// </summary>
public sealed class BudgetGuardJob
{
    private readonly ILogger<BudgetGuardJob> _logger;

    public BudgetGuardJob(ILogger<BudgetGuardJob> logger)
    {
        _logger = logger;
    }

    public Task Execute()
    {
        _logger.LogInformation("BudgetGuardJob: budget check triggered (spend data not yet available).");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Weekly outcome tracker (Sunday 06:00 UTC).
/// Labels past decisions as positive/negative/neutral based on available signals.
/// Phase 2: assigns "neutral" to decisions older than 7 days with no outcome.
/// Phase 4: replace stub with ROAS delta from Meta Insights API.
/// </summary>
public sealed class OutcomeTrackerJob
{
    private readonly PlatformDbContext         _db;
    private readonly IDecisionMemoryService    _decisionMemory;
    private readonly ILogger<OutcomeTrackerJob> _logger;

    public OutcomeTrackerJob(
        PlatformDbContext db,
        IDecisionMemoryService decisionMemory,
        ILogger<OutcomeTrackerJob> logger)
    {
        _db             = db;
        _decisionMemory = decisionMemory;
        _logger         = logger;
    }

    public async Task Execute()
    {
        _logger.LogInformation("OutcomeTrackerJob started at {UtcNow:u}", DateTime.UtcNow);

        var cutoff = DateTime.UtcNow.AddDays(-7);

        // Find decisions older than 7 days that have no outcome yet
        var pending = await _db.AgentDecisions
            .Where(d => d.OutcomeLabel == null && d.CreatedAt < cutoff)
            .ToListAsync();

        if (pending.Count == 0)
        {
            _logger.LogInformation("OutcomeTrackerJob: no pending decisions to label.");
            return;
        }

        // Phase 2 stub: label all as "neutral" until real Meta ROAS data is wired
        foreach (var decision in pending)
            decision.SetOutcome("neutral");

        await _db.SaveChangesAsync();

        // Now trigger embedding for all just-labelled decisions so Phase 3 can use them
        foreach (var decision in pending)
        {
            try
            {
                await _decisionMemory.EmbedDecisionAsync(decision.Id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OutcomeTrackerJob: embed failed for decision {Id}.", decision.Id);
            }
        }

        _logger.LogInformation("OutcomeTrackerJob: labelled {Count} decision(s) as neutral.", pending.Count);
    }
}
