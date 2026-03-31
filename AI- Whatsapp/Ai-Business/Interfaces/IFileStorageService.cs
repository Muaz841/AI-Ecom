using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a product image and returns its public-relative URL (e.g. /uploads/products/{tenantId}/{file}).
    /// </summary>
    Task<string> SaveProductImageAsync(
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Saves a pose reference image and returns its public-relative URL (e.g. /uploads/poses/{tenantId}/{file}).
    /// </summary>
    Task<string> SavePoseImageAsync(
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
