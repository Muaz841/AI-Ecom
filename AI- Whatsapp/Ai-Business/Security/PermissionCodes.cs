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
        WebhooksManage
    };
}
