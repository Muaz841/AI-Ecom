using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;

namespace EcomAI.Platform.Business.Interfaces;

// ── Repository ────────────────────────────────────────────────────────────────

public interface IPlatformMarketingConfigRepository
{
    Task<PlatformMarketingConfig?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PlatformMarketingConfig config, CancellationToken cancellationToken = default);
}

public interface IKnowledgeChunkRepository
{
    Task<List<KnowledgeChunk>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<KnowledgeChunk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(KnowledgeChunk chunk, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all active chunks that already have an EmbeddingJson, for RAG retrieval.</summary>
    Task<List<KnowledgeChunkVector>> GetWithEmbeddingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IAgentDecisionRepository
{
    Task<List<AgentDecision>> GetRecentAsync(Guid tenantId, int count = 20, CancellationToken cancellationToken = default);
    Task<AgentDecision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AgentDecision decision, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns decisions that have both EmbeddingJson and OutcomeLabel set, for Phase 3 memory RAG.</summary>
    Task<List<AgentDecisionVector>> GetWithEmbeddingsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns decisions missing EmbeddingJson, for the embedding backfill job.</summary>
    Task<List<AgentDecision>> GetPendingEmbeddingAsync(Guid tenantId, int maxRows = 50, CancellationToken cancellationToken = default);

    /// <summary>Returns total count of decisions that have OutcomeLabel set, used for Phase 3 activation check.</summary>
    Task<int> CountWithOutcomesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

// ── Marketing Config DTOs ─────────────────────────────────────────────────────

public sealed record PlatformMarketingConfigResult(
    bool IsConfigured,
    bool ClaudeApiKeySet,
    string? ClaudeApiKeyMasked,
    string ClaudeDecisionModel,
    string ClaudeSummaryModel,
    string? MetaAdsAccountId,
    bool MetaAdsAccessTokenSet,
    string? MetaAdsAccessTokenMasked,
    bool DryRun,
    int MaxActionsPerDay,
    decimal DailySpendCapUsd,
    string? UpdatedAt);

public sealed record SavePlatformMarketingConfigRequest(
    string? ClaudeApiKey,
    string ClaudeDecisionModel,
    string ClaudeSummaryModel,
    string? MetaAdsAccountId,
    string? MetaAdsAccessToken,
    bool DryRun,
    int MaxActionsPerDay,
    decimal DailySpendCapUsd);

public interface IPlatformMarketingSettingsService
{
    Task<PlatformMarketingConfigResult> GetAsync(CancellationToken cancellationToken = default);
    Task<PlatformMarketingConfigResult> SaveAsync(SavePlatformMarketingConfigRequest request, CancellationToken cancellationToken = default);
}

// ── Knowledge DTOs ────────────────────────────────────────────────────────────

public sealed record KnowledgeChunkDto(
    Guid Id,
    string Title,
    string Content,
    string? Source,
    DateTime CreatedAt);

public sealed record AddKnowledgeRequest(
    string Title,
    string Content,
    string? Source = null);

public sealed record UpdateKnowledgeRequest(
    string Title,
    string Content,
    string? Source = null);

public interface IKnowledgeService
{
    Task<List<KnowledgeChunkDto>> GetChunksAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeChunkDto> AddChunkAsync(AddKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task<KnowledgeChunkDto> UpdateChunkAsync(Guid id, UpdateKnowledgeRequest request, CancellationToken cancellationToken = default);
    Task DeleteChunkAsync(Guid id, CancellationToken cancellationToken = default);
}

// ── Agent Decision DTOs ───────────────────────────────────────────────────────

public sealed record AgentDecisionDto(
    Guid Id,
    DateTime RunAt,
    string ActionType,
    string? ActionPayload,
    string Status,
    string? Reason,
    double Confidence,
    bool IsDryRun,
    DateTime? ExecutedAt,
    DateTime CreatedAt);

public interface IAgentDecisionService
{
    Task<List<AgentDecisionDto>> GetRecentAsync(int count = 20, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid id, CancellationToken cancellationToken = default);
}
