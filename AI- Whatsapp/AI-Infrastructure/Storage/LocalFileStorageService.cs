using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace EcomAI.Platform.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new([".jpg", ".jpeg", ".png", ".webp", ".gif"], StringComparer.OrdinalIgnoreCase);

    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public Task<string> SaveProductImageAsync(
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
        => SaveFileAsync(tenantId, fileStream, fileName, contentType, "products", ct);

    public Task<string> SavePoseImageAsync(
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
        => SaveFileAsync(tenantId, fileStream, fileName, contentType, "poses", ct);

    private async Task<string> SaveFileAsync(
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        string category,
        CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException(
                $"File type '{ext}' is not allowed. Accepted: {string.Join(", ", AllowedExtensions)}");

        var webRoot = _env.WebRootPath
            ?? Path.Combine(_env.ContentRootPath, "wwwroot");

        var folder = Path.Combine(webRoot, "uploads", category, tenantId.ToString());
        Directory.CreateDirectory(folder);

        var uniqueName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var fullPath   = Path.Combine(folder, uniqueName);

        await using var fs = File.Create(fullPath);
        await fileStream.CopyToAsync(fs, ct);

        return $"/uploads/{category}/{tenantId}/{uniqueName}";
    }
}
