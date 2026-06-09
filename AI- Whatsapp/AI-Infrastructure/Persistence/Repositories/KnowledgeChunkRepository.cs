using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.Persistence.Repositories;

public sealed class KnowledgeChunkRepository : IKnowledgeChunkRepository
{
    private readonly PlatformDbContext _db;

    public KnowledgeChunkRepository(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<List<KnowledgeChunk>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _db.KnowledgeChunks
              .AsNoTracking()
              .Where(c => c.TenantId == tenantId && c.IsActive)
              .OrderByDescending(c => c.CreatedAt)
              .ToListAsync(cancellationToken);

    public Task<List<KnowledgeChunkVector>> GetWithEmbeddingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _db.KnowledgeChunks
              .AsNoTracking()
              .Where(c => c.TenantId == tenantId && c.IsActive && c.EmbeddingJson != null)
              .Select(c => new KnowledgeChunkVector(c.Id, c.Title, c.Content, c.EmbeddingJson!))
              .ToListAsync(cancellationToken);

    public Task<KnowledgeChunk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.KnowledgeChunks
              .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(KnowledgeChunk chunk, CancellationToken cancellationToken = default)
        => await _db.KnowledgeChunks.AddAsync(chunk, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
