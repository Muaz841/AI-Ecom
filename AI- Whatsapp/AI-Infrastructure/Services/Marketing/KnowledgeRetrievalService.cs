using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// Phase 2 — Retrieves the most relevant knowledge chunks for a query using
/// Gemini embeddings + in-process cosine similarity.
/// Falls back to recency-ordered chunks when embeddings are unavailable.
/// </summary>
public sealed class KnowledgeRetrievalService : IKnowledgeRetrievalService
{
    private readonly IKnowledgeChunkRepository        _repo;
    private readonly IEmbeddingService                _embedding;
    private readonly IVectorStore                     _vectorStore;
    private readonly ILogger<KnowledgeRetrievalService> _logger;

    public KnowledgeRetrievalService(
        IKnowledgeChunkRepository repo,
        IEmbeddingService embedding,
        IVectorStore vectorStore,
        ILogger<KnowledgeRetrievalService> logger)
    {
        _repo        = repo;
        _embedding   = embedding;
        _vectorStore = vectorStore;
        _logger      = logger;
    }

    public async Task<List<RelevantChunk>> RetrieveAsync(
        Guid tenantId,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Embed the incoming query
        var queryVector = await _embedding.GetEmbeddingAsync(query, cancellationToken);

        // Fetch chunks that already have embeddings
        var chunks = await _repo.GetWithEmbeddingsAsync(tenantId, cancellationToken);

        if (queryVector is null || chunks.Count == 0)
        {
            // Fallback: return the most recent active chunks without scoring
            _logger.LogDebug("KnowledgeRetrieval fallback (no embeddings). TenantId={TenantId}", tenantId);
            var fallback = await _repo.GetActiveAsync(tenantId, cancellationToken);
            return fallback
                .Take(topK)
                .Select(c => new RelevantChunk(c.Title, c.Content, 0f))
                .ToList();
        }

        // Score all chunks and return top-k
        var scored = chunks
            .Select(c =>
            {
                float score = 0f;
                try
                {
                    var vec = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson);
                    if (vec is not null)
                        score = _vectorStore.CosineSimilarity(queryVector, vec);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize embedding for chunk {ChunkId}.", c.Id);
                }
                return (c, score);
            })
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => new RelevantChunk(x.c.Title, x.c.Content, x.score))
            .ToList();

        _logger.LogDebug("KnowledgeRetrieval complete. TenantId={TenantId} Candidates={Total} TopK={TopK}",
            tenantId, chunks.Count, scored.Count);

        return scored;
    }
}
