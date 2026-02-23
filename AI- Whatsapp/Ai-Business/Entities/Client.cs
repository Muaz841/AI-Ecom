using System;

namespace EcomAI.Platform.Business.Entities;

public class Client : Entity<Guid>
{
    public string Name { get; private set; } = null!;
    public string BusinessName { get; private set; } = null!;
    public string MetaAccessToken { get; private set; } = null!;
    public string MetaPageId { get; private set; } = null!;
    public string WhatsAppBusinessAccountId { get; private set; } = null!;
    public string? ShopifyStoreId { get; private set; }
    public string? WooCommerceStoreId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    private Client()
    {
    }

    public static Client Create(
        string name,
        string businessName,
        string metaAccessToken,
        string metaPageId,
        string whatsAppBusinessAccountId,
        string? shopifyStoreId = null,
        string? wooCommerceStoreId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name required", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new ArgumentException("Business name required", nameof(businessName));
        }

        if (string.IsNullOrWhiteSpace(metaAccessToken))
        {
            throw new ArgumentException("Meta access token required", nameof(metaAccessToken));
        }

        if (string.IsNullOrWhiteSpace(metaPageId))
        {
            throw new ArgumentException("Meta Page ID required", nameof(metaPageId));
        }

        if (string.IsNullOrWhiteSpace(whatsAppBusinessAccountId))
        {
            throw new ArgumentException("WhatsApp Business Account ID required", nameof(whatsAppBusinessAccountId));
        }

        return new Client
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            BusinessName = businessName.Trim(),
            MetaAccessToken = metaAccessToken,
            MetaPageId = metaPageId.Trim(),
            WhatsAppBusinessAccountId = whatsAppBusinessAccountId.Trim(),
            ShopifyStoreId = shopifyStoreId?.Trim(),
            WooCommerceStoreId = wooCommerceStoreId?.Trim(),
            CreatedAt = DateTime.UtcNow,
            LastSyncedAt = null
        };
    }

    public void UpdateSyncTimestamp()
    {
        LastSyncedAt = DateTime.UtcNow;
    }
}
