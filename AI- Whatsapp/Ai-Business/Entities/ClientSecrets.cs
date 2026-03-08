using System;

namespace EcomAI.Platform.Business.Entities;

public sealed class ClientSecrets : Entity<Guid>, ITenantEntity
{
    public Guid TenantRefId { get; private set; }
    public string? MetaAccessToken { get; private set; }
    public string? MetaPageId { get; private set; }
    public string? WhatsAppBusinessAccountId { get; private set; }
    public string? ShopifyStoreId { get; private set; }
    public string? WooCommerceStoreId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    private ClientSecrets()
    {
    }

    public static ClientSecrets CreateForTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId required.", nameof(tenantId));
        }

        return new ClientSecrets
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantRefId = tenantId,
            CreatedAt = DateTime.UtcNow,
            LastSyncedAt = null
        };
    }

    public void UpdateSyncTimestamp()
    {
        LastSyncedAt = DateTime.UtcNow;
    }

    public void UpdateStoreConfiguration(
        string? shopifyStoreId = null,
        string? wooCommerceStoreId = null)
    {
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
