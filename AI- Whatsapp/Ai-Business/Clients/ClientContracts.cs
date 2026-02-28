using System;

namespace EcomAI.Platform.Business.Clients;

public record ClientDto(
    Guid Id,
    string Name,
    string BusinessName,
    string MetaPageId,
    string WhatsAppBusinessAccountId,
    string? ShopifyStoreId,
    string? WooCommerceStoreId,
    DateTime CreatedAt,
    DateTime? LastSyncedAt);
