using System;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// In-process cosine similarity.
/// Safe for up to ~1 000 vectors; swap for a dedicated vector store at scale.
/// </summary>
public sealed class InProcessVectorStore : IVectorStore
{
    public float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimension.");

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot   += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0f;
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
