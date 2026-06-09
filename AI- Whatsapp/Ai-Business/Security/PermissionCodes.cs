namespace EcomAI.Platform.Business.Security;

public static class PermissionCodes
{
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string PermissionsManage = "permissions.manage";
    public const string ClientsManage = "clients.manage";
    public const string ProductsManage = "products.manage";
    public const string ConversationsRead = "conversations.read";
    public const string ConversationsManage = "conversations.manage";
    public const string LogsRead = "logs.read";
    public const string AiManage = "ai.manage";
    public const string WebhooksManage = "webhooks.manage";
    public const string IntegrationsRead = "integrations.read";
    public const string IntegrationsManage = "integrations.manage";

    public const string TenantsManage = "tenants.manage";
    public const string SubscriptionsManage = "subscriptions.manage";
    public const string PlatformSettings = "platform.settings";

    public const string ImagesGenerate = "images.generate";
    public const string ImagesRead     = "images.read";
    public const string ImagesManage   = "images.manage";

    public const string MetaAdsView    = "meta.ads.view";
    public const string MetaAdsManage  = "meta.ads.manage";
    public const string MetaAdsApprove = "meta.ads.approve";

    public static readonly string[] All =
    {
        UsersManage,
        RolesManage,
        PermissionsManage,
        ClientsManage,
        ProductsManage,
        ConversationsRead,
        ConversationsManage,
        LogsRead,
        AiManage,
        WebhooksManage,
        IntegrationsRead,
        IntegrationsManage,
        TenantsManage,
        SubscriptionsManage,
        PlatformSettings,
        ImagesGenerate,
        ImagesRead,
        ImagesManage,
        MetaAdsView,
        MetaAdsManage,
        MetaAdsApprove,
    };

    public static readonly string[] HostOnly =
    {
        TenantsManage,
        SubscriptionsManage,
        PlatformSettings
    };

    public static readonly string[] TenantScoped =
    {
        UsersManage,
        RolesManage,
        PermissionsManage,
        ClientsManage,
        ProductsManage,
        ConversationsRead,
        ConversationsManage,
        LogsRead,
        AiManage,
        WebhooksManage,
        IntegrationsRead,
        IntegrationsManage,
        ImagesGenerate,
        ImagesRead,
        ImagesManage,
        MetaAdsView,
        MetaAdsManage,
        MetaAdsApprove,
    };
}
