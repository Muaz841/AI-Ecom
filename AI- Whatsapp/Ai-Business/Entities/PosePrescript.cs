using System;

namespace EcomAI.Platform.Business.Entities;

/// <summary>
/// A confirmed pose extracted from a reference image and saved to the tenant's pose library.
/// Implements ITenantEntity — the global EF query filter scopes all queries to the current tenant.
/// Soft-deleted records (IsActive = false) are excluded from the library listing.
/// </summary>
public sealed class PosePrescript : Entity<Guid>, ITenantEntity
{
    public Guid     TenantId           { get; private set; }
    public string   Name               { get; private set; } = string.Empty;

    /// <summary>Full textual pose description returned by the AI vision model.</summary>
    public string   PoseScript         { get; private set; } = string.Empty;

    /// <summary>Storage path to the reference image used for extraction (via IFileStorageService).</summary>
    public string   ReferenceImagePath { get; private set; } = string.Empty;

    public DateTime CreatedAt          { get; private set; }
    public Guid     CreatedByUserId    { get; private set; }
    public bool     IsActive           { get; private set; }

    private PosePrescript() { }

    public static PosePrescript Create(
        Guid   tenantId,
        string name,
        string poseScript,
        string referenceImagePath,
        Guid   createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Pose name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(poseScript))
            throw new ArgumentException("Pose script is required.", nameof(poseScript));
        if (string.IsNullOrWhiteSpace(referenceImagePath))
            throw new ArgumentException("Reference image path is required.", nameof(referenceImagePath));

        return new PosePrescript
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            Name               = name.Trim(),
            PoseScript         = poseScript.Trim(),
            ReferenceImagePath = referenceImagePath,
            CreatedAt          = DateTime.UtcNow,
            CreatedByUserId    = createdByUserId,
            IsActive           = true,
        };
    }

    public void Deactivate() => IsActive = false;
}
