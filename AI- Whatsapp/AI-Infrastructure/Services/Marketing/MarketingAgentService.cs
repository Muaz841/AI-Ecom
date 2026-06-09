using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// Full marketing agent orchestration for a single tenant run:
///   1. Load performance context (stub — real Meta Ads data in Phase 4)
///   2. Phase 2: Retrieve top-k knowledge chunks via RAG
///   3. Phase 3: Retrieve top-k past decisions via decision memory (when active)
///   4. Load system prompt from skill files
///   5. Call Claude → parse decision
///   6. Save AgentDecision
///   7. Fire-and-forget: embed the new decision for future memory
/// </summary>
public sealed class MarketingAgentService : IMarketingAgentService
{
    private readonly IPlatformMarketingConfigRepository _marketingConfigRepo;
    private readonly IAgentDecisionRepository          _decisionRepo;
    private readonly IKnowledgeRetrievalService        _knowledgeRetrieval;
    private readonly IDecisionMemoryService            _decisionMemory;
    private readonly ISkillLoaderService               _skillLoader;
    private readonly IClaudeDecisionService            _claude;
    private readonly ILogger<MarketingAgentService>    _logger;

    public MarketingAgentService(
        IPlatformMarketingConfigRepository marketingConfigRepo,
        IAgentDecisionRepository decisionRepo,
        IKnowledgeRetrievalService knowledgeRetrieval,
        IDecisionMemoryService decisionMemory,
        ISkillLoaderService skillLoader,
        IClaudeDecisionService claude,
        ILogger<MarketingAgentService> logger)
    {
        _marketingConfigRepo = marketingConfigRepo;
        _decisionRepo        = decisionRepo;
        _knowledgeRetrieval  = knowledgeRetrieval;
        _decisionMemory      = decisionMemory;
        _skillLoader         = skillLoader;
        _claude              = claude;
        _logger              = logger;
    }

    public async Task RunForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MarketingAgent starting. TenantId={TenantId}", tenantId);

        try
        {
            // ── 1. Guard: marketing engine must be configured ──────────────────────
            var cfg = await _marketingConfigRepo.GetAsync(cancellationToken);
            if (cfg is null || !cfg.IsConfigured)
            {
                _logger.LogWarning("Marketing engine not configured. Skipping tenant {TenantId}.", tenantId);
                return;
            }

            // ── 2. Performance context (stub — replace with Meta Ads data) ─────────
            var performanceContext = BuildStubPerformanceContext(tenantId);

            // ── 3. Phase 2: Knowledge retrieval ────────────────────────────────────
            var knowledge = await _knowledgeRetrieval.RetrieveAsync(
                tenantId,
                performanceContext,
                topK: 5,
                cancellationToken);

            // ── 4. Phase 3: Decision memory retrieval ──────────────────────────────
            var pastDecisions = await _decisionMemory.RetrieveAsync(
                tenantId,
                performanceContext,
                topK: 3,
                cancellationToken);

            // ── 5. Build system prompt + user context ──────────────────────────────
            var systemPrompt = await _skillLoader.LoadSystemPromptAsync(cancellationToken);
            var userContext  = BuildUserContext(performanceContext, knowledge, pastDecisions, cfg);

            // ── 6. Call Claude ─────────────────────────────────────────────────────
            var decision = await _claude.DecideAsync(systemPrompt, userContext, cancellationToken);

            // ── 7. Save AgentDecision ──────────────────────────────────────────────
            var entity = AgentDecision.Create(
                tenantId,
                contextSummary:   TruncateContext(userContext),
                actionType:       decision.ActionType,
                actionPayload:    decision.ActionPayload,
                reason:           decision.Reason,
                confidence:       decision.Confidence,
                requiresApproval: decision.RequiresApproval,
                isDryRun:         cfg.DryRun);

            await _decisionRepo.AddAsync(entity, cancellationToken);
            await _decisionRepo.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "AgentDecision saved. TenantId={TenantId} Action={Action} Status={Status} DryRun={DryRun}",
                tenantId, entity.ActionType, entity.Status, entity.IsDryRun);

            // ── 8. Async: embed the new decision for future Phase 3 retrieval ──────
            // Fire and forget — failure here does not break the run
            _ = EmbedDecisionSafeAsync(entity.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarketingAgent run failed for tenant {TenantId}.", tenantId);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string BuildStubPerformanceContext(Guid tenantId)
    {
        // Phase 1 stub — real context will be injected from Meta Ads API in Phase 4
        return $"[Performance Context for tenant {tenantId}]\n" +
               "No live performance data available yet. " +
               "Base recommendations on knowledge base content and conservative best practices.";
    }

    private static string BuildUserContext(
        string performanceContext,
        List<RelevantChunk> knowledge,
        List<RelevantDecision> pastDecisions,
        PlatformMarketingConfig cfg)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Campaign Performance Context");
        sb.AppendLine(performanceContext);
        sb.AppendLine();

        if (knowledge.Count > 0)
        {
            sb.AppendLine("## Brand Knowledge Base (most relevant)");
            foreach (var chunk in knowledge)
            {
                sb.AppendLine($"### {chunk.Title}");
                sb.AppendLine(chunk.Content);
                sb.AppendLine();
            }
        }

        if (pastDecisions.Count > 0)
        {
            sb.AppendLine("## Past Decisions (most similar situations)");
            foreach (var d in pastDecisions)
            {
                var outcome = d.OutcomeLabel ?? "pending";
                sb.AppendLine($"- {d.RunAt:yyyy-MM-dd}: {d.ActionType} (confidence={d.Confidence:P0}, outcome={outcome})");
                if (!string.IsNullOrWhiteSpace(d.Reason))
                    sb.AppendLine($"  Reason: {d.Reason}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Safety Constraints");
        sb.AppendLine($"- Max actions per day: {cfg.MaxActionsPerDay}");
        sb.AppendLine($"- Daily spend cap: ${cfg.DailySpendCapUsd:F2} USD");
        sb.AppendLine($"- Dry run mode: {cfg.DryRun} (when true, no changes are sent to Meta)");
        sb.AppendLine();

        sb.AppendLine("## Required Output Format");
        sb.AppendLine("""
            Respond with a JSON object only:
            {
              "action_type": "scale_up | scale_down | pause | activate | no_action",
              "action_payload": { /* optional details */ },
              "reason": "Brief explanation",
              "confidence": 0.85,
              "requires_approval": true
            }
            """);

        return sb.ToString();
    }

    private static string TruncateContext(string context)
    {
        const int maxLen = 500;
        return context.Length <= maxLen ? context : context[..maxLen] + "…";
    }

    private async Task EmbedDecisionSafeAsync(Guid decisionId, CancellationToken cancellationToken)
    {
        try
        {
            await _decisionMemory.EmbedDecisionAsync(decisionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision embedding failed (non-critical). DecisionId={Id}", decisionId);
        }
    }
}
