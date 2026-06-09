using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

// ── Embedding service abstraction ─────────────────────────────────────────────
// Implemented by GeminiEmbeddingService using text-embedding-004 (768-dim).

public interface IEmbeddingService
{
    /// <summary>Returns a 768-dim float vector for the given text, or null on failure.</summary>
    Task<float[]?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

// ── In-process vector store ───────────────────────────────────────────────────
// Cosine similarity over in-memory float arrays.
// Safe for < 1 000 chunks; swap for pgvector/Azure Cognitive Search at scale.

public interface IVectorStore
{
    float CosineSimilarity(float[] a, float[] b);
}

// ── Knowledge retrieval (Phase 2) ─────────────────────────────────────────────

public sealed record RelevantChunk(string Title, string Content, float Score);

public interface IKnowledgeRetrievalService
{
    /// <summary>
    /// Embeds <paramref name="query"/> and returns the top-<paramref name="topK"/>
    /// knowledge chunks by cosine similarity for the given tenant.
    /// Falls back to the most-recently-updated chunks when embeddings are unavailable.
    /// </summary>
    Task<List<RelevantChunk>> RetrieveAsync(
        Guid tenantId,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

// ── Decision memory (Phase 3) ─────────────────────────────────────────────────

public sealed record RelevantDecision(
    string ActionType,
    string? Reason,
    double Confidence,
    string? OutcomeLabel,
    DateTime RunAt,
    float Score);

public interface IDecisionMemoryService
{
    /// <summary>
    /// Returns the top-<paramref name="topK"/> past decisions most similar to
    /// <paramref name="query"/> for the given tenant.
    /// Returns empty list when Phase 3 is not yet active (< MinDecisions).
    /// </summary>
    Task<List<RelevantDecision>> RetrieveAsync(
        Guid tenantId,
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);

    /// <summary>Embeds and persists the embedding for a single decision.</summary>
    Task EmbedDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default);
}

// ── Skill loader ──────────────────────────────────────────────────────────────

public interface ISkillLoaderService
{
    /// <summary>Returns all skill markdown files concatenated as a single system-prompt block.</summary>
    Task<string> LoadSystemPromptAsync(CancellationToken cancellationToken = default);
}

// ── Claude decision service ───────────────────────────────────────────────────

public sealed record ClaudeDecisionResult(
    string ActionType,
    string? ActionPayload,
    string? Reason,
    double Confidence,
    bool RequiresApproval);

public interface IClaudeDecisionService
{
    /// <summary>
    /// Sends the assembled context to Claude and parses the JSON decision response.
    /// Uses the model from <see cref="IPlatformMarketingConfigRepository"/>.
    /// Returns a fallback <c>no_action</c> result on any failure.
    /// </summary>
    Task<ClaudeDecisionResult> DecideAsync(
        string systemPrompt,
        string userContext,
        CancellationToken cancellationToken = default);
}

// ── Marketing agent orchestration ─────────────────────────────────────────────

public interface IMarketingAgentService
{
    /// <summary>
    /// Runs the full daily marketing agent cycle for a single tenant:
    /// 1. Fetch performance context
    /// 2. Retrieve top-k knowledge (Phase 2)
    /// 3. Retrieve decision memory (Phase 3 when active)
    /// 4. Call Claude → parse decision
    /// 5. Save AgentDecision + embed decision async
    /// </summary>
    Task RunForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

// ── Extended repository queries needed for RAG ────────────────────────────────

public sealed record KnowledgeChunkVector(
    Guid Id,
    string Title,
    string Content,
    string EmbeddingJson);

public sealed record AgentDecisionVector(
    Guid Id,
    string ActionType,
    string? Reason,
    double Confidence,
    string? OutcomeLabel,
    DateTime RunAt,
    string EmbeddingJson);
