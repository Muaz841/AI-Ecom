using System;

namespace EcomAI.Platform.Business.Entities;

public sealed class Tenant : Entity<Guid>, ITenantEntity
{
    public string Name { get; private set; } = null!;
    public string BusinessName { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    private Tenant()
    {
    }

    public static Tenant Create(string name, string businessName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name required", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new ArgumentException("Business name required", nameof(businessName));
        }

        var id = Guid.NewGuid();
        return new Tenant
        {
            Id = id,
            TenantId = id,
            Name = name.Trim(),
            BusinessName = businessName.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string name, string businessName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name required", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new ArgumentException("Business name required", nameof(businessName));
        }

        Name = name.Trim();
        BusinessName = businessName.Trim();
    }

    public void UpdateSyncTimestamp()
    {
        LastSyncedAt = DateTime.UtcNow;
    }
}
