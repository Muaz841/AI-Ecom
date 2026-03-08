using System;

namespace EcomAI.Platform.Business.Entities;

public class Client : Entity<Guid>, ITenantEntity
{
    public string Name { get; private set; } = null!;
    public string BusinessName { get; private set; } = null!;
    public string? MetaAccessToken { get; private set; }
    public string? MetaPageId { get; private set; }
    public string? WhatsAppBusinessAccountId { get; private set; }
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
        string? metaAccessToken = null,
        string? metaPageId = null,
        string? whatsAppBusinessAccountId = null,
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

        var id = Guid.NewGuid();

        return new Client
        {
            Id = id,
            TenantId = id,
            Name = name.Trim(),
            BusinessName = businessName.Trim(),
            MetaAccessToken = string.IsNullOrWhiteSpace(metaAccessToken) ? null : metaAccessToken.Trim(),
            MetaPageId = string.IsNullOrWhiteSpace(metaPageId) ? null : metaPageId.Trim(),
            WhatsAppBusinessAccountId = string.IsNullOrWhiteSpace(whatsAppBusinessAccountId) ? null : whatsAppBusinessAccountId.Trim(),
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

    public void UpdateProfile(
        string name,
        string businessName,
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

        Name = name.Trim();
        BusinessName = businessName.Trim();
        ShopifyStoreId = shopifyStoreId?.Trim();
        WooCommerceStoreId = wooCommerceStoreId?.Trim();
    }

    public void UpdateMetaConfiguration(
        string? metaAccessToken,
        string? metaPageId,
        string? whatsAppBusinessAccountId)
    {
        MetaAccessToken = string.IsNullOrWhiteSpace(metaAccessToken) ? null : metaAccessToken.Trim();
        MetaPageId = string.IsNullOrWhiteSpace(metaPageId) ? null : metaPageId.Trim();
        WhatsAppBusinessAccountId = string.IsNullOrWhiteSpace(whatsAppBusinessAccountId) ? null : whatsAppBusinessAccountId.Trim();
    }
}
