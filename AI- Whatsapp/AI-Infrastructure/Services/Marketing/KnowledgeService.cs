using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

public sealed class KnowledgeService : IKnowledgeService
{
    private readonly IKnowledgeChunkRepository _repo;
    private readonly ICurrentTenantAccessor    _tenantAccessor;
    private readonly IEmbeddingService         _embedding;
    private readonly ILogger<KnowledgeService> _logger;

    public KnowledgeService(
        IKnowledgeChunkRepository repo,
        ICurrentTenantAccessor tenantAccessor,
        IEmbeddingService embedding,
        ILogger<KnowledgeService> logger)
    {
        _repo           = repo;
        _tenantAccessor = tenantAccessor;
        _embedding      = embedding;
        _logger         = logger;
    }

    public async Task<List<KnowledgeChunkDto>> GetChunksAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var chunks   = await _repo.GetActiveAsync(tenantId, cancellationToken);
        return chunks.Select(Map).ToList();
    }

    public async Task<KnowledgeChunkDto> AddChunkAsync(
        AddKnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var chunk    = KnowledgeChunk.Create(tenantId, request.Title, request.Content, request.Source);
        await _repo.AddAsync(chunk, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        _ = EmbedChunkSafeAsync(chunk, cancellationToken); // fire-and-forget
        return Map(chunk);
    }

    public async Task<KnowledgeChunkDto> UpdateChunkAsync(
        Guid id,
        UpdateKnowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var chunk = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Knowledge chunk {id} not found.");

        chunk.Update(request.Title, request.Content, request.Source);
        await _repo.SaveChangesAsync(cancellationToken);
        _ = EmbedChunkSafeAsync(chunk, cancellationToken); // fire-and-forget
        return Map(chunk);
    }

    public async Task DeleteChunkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chunk = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Knowledge chunk {id} not found.");

        chunk.Deactivate();
        await _repo.SaveChangesAsync(cancellationToken);
    }

    private async Task EmbedChunkSafeAsync(KnowledgeChunk chunk, CancellationToken cancellationToken)
    {
        try
        {
            var text   = $"{chunk.Title}\n{chunk.Content}";
            var vector = await _embedding.GetEmbeddingAsync(text, cancellationToken);
            if (vector is null) return;

            chunk.SetEmbedding(JsonSerializer.Serialize(vector));
            await _repo.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Knowledge chunk embedded. ChunkId={Id}", chunk.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to embed knowledge chunk {Id} (non-critical).", chunk.Id);
        }
    }

    private Guid RequireTenantId() =>
        _tenantAccessor.GetCurrentTenantId()
            ?? throw new InvalidOperationException("Tenant context is required for this operation.");

    private static KnowledgeChunkDto Map(KnowledgeChunk c) =>
        new(c.Id, c.Title, c.Content, c.Source, c.CreatedAt);
}
