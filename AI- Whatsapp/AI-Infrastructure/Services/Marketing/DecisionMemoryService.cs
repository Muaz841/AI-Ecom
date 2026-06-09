using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// Phase 3 — Stores and retrieves decision memory for the marketing agent.
/// Activates when the tenant has ≥ MarketingEngine:Phase3MinDecisions decisions with outcomes.
/// </summary>
public sealed class DecisionMemoryService : IDecisionMemoryService
{
    private readonly IAgentDecisionRepository          _repo;
    private readonly IEmbeddingService                 _embedding;
    private readonly IVectorStore                      _vectorStore;
    private readonly IConfiguration                   _config;
    private readonly ILogger<DecisionMemoryService>    _logger;

    private int Phase3MinDecisions =>
        _config.GetValue<int>("MarketingEngine:Phase3MinDecisions", 30);

    public DecisionMemoryService(
        IAgentDecisionRepository repo,
        IEmbeddingService embedding,
        IVectorStore vectorStore,
        IConfiguration config,
        ILogger<DecisionMemoryService> logger)
    {
        _repo        = repo;
        _embedding   = embedding;
        _vectorStore = vectorStore;
        _config      = config;
        _logger      = logger;
    }

    public async Task<List<RelevantDecision>> RetrieveAsync(
        Guid tenantId,
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        // Check Phase 3 activation threshold
        var count = await _repo.CountWithOutcomesAsync(tenantId, cancellationToken);
        if (count < Phase3MinDecisions)
        {
            _logger.LogDebug("Phase 3 inactive for tenant {TenantId}. OutcomeCount={Count} Min={Min}",
                tenantId, count, Phase3MinDecisions);
            return [];
        }

        var queryVector = await _embedding.GetEmbeddingAsync(query, cancellationToken);
        if (queryVector is null) return [];

        var decisions = await _repo.GetWithEmbeddingsAsync(tenantId, cancellationToken);
        if (decisions.Count == 0) return [];

        var scored = decisions
            .Select(d =>
            {
                float score = 0f;
                try
                {
                    var vec = JsonSerializer.Deserialize<float[]>(d.EmbeddingJson);
                    if (vec is not null)
                        score = _vectorStore.CosineSimilarity(queryVector, vec);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize embedding for decision {DecisionId}.", d.Id);
                }
                return (d, score);
            })
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => new RelevantDecision(
                x.d.ActionType,
                x.d.Reason,
                x.d.Confidence,
                x.d.OutcomeLabel,
                x.d.RunAt,
                x.score))
            .ToList();

        _logger.LogDebug("DecisionMemory retrieved. TenantId={TenantId} Candidates={Total} TopK={K}",
            tenantId, decisions.Count, scored.Count);

        return scored;
    }

    public async Task EmbedDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default)
    {
        var decision = await _repo.GetByIdAsync(decisionId, cancellationToken);
        if (decision is null)
        {
            _logger.LogWarning("EmbedDecisionAsync: decision {Id} not found.", decisionId);
            return;
        }

        // Build a text representation of the decision for embedding
        var text = $"Action: {decision.ActionType}. Reason: {decision.Reason ?? "N/A"}. Confidence: {decision.Confidence:P0}.";

        var vector = await _embedding.GetEmbeddingAsync(text, cancellationToken);
        if (vector is null) return;

        var json = JsonSerializer.Serialize(vector);
        decision.SetEmbedding(json);
        await _repo.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Decision embedded. DecisionId={Id}", decisionId);
    }
}
