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

    public async Task<string> SaveProductImageAsync(
        Guid tenantId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException(
                $"File type '{ext}' is not allowed. Accepted: {string.Join(", ", AllowedExtensions)}");

        var webRoot = _env.WebRootPath
            ?? Path.Combine(_env.ContentRootPath, "wwwroot");

        var folder = Path.Combine(webRoot, "uploads", "products", tenantId.ToString());
        Directory.CreateDirectory(folder);

        var uniqueName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var fullPath   = Path.Combine(folder, uniqueName);

        await using var fs = File.Create(fullPath);
        await fileStream.CopyToAsync(fs, ct);

        return $"/uploads/products/{tenantId}/{uniqueName}";
    }
}
