using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public sealed class MetaIntegrationService : IMetaIntegrationService
{
    public const string MetaOAuthHttpClientName = "MetaOAuthApi";

    // Scopes requested per channel during OAuth
    private static readonly Dictionary<string, string[]> ChannelScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        [MetaChannelTypes.Instagram] =
        [
            "instagram_business_basic",
            "instagram_business_manage_messages",
            "instagram_business_manage_comments",
            "pages_show_list",
            "pages_manage_metadata",
            "pages_messaging"
        ],
        [MetaChannelTypes.Facebook] =
        [
            "pages_show_list",
            "pages_messaging",
            "pages_manage_metadata"
        ],
        [MetaChannelTypes.WhatsApp] =
        [
            "whatsapp_business_messaging",
            "whatsapp_business_management"
        ]
    };

    // Webhook fields subscribed per-page for Instagram DMs
    private const string InstagramPageWebhookFields =
        "messages,messaging_optins,messaging_postbacks,message_reads,message_deliveries,messaging_seen";

    private readonly PlatformDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenProtector _tokenProtector;
    private readonly IApplicationLogger _logger;
    private readonly IMetaOAuthRuntimeConfigProvider _configProvider;

    public MetaIntegrationService(
        PlatformDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ITokenProtector tokenProtector,
        IApplicationLogger logger,
        IMetaOAuthRuntimeConfigProvider configProvider)
    {
        _dbContext        = dbContext;
        _httpClientFactory = httpClientFactory;
        _tokenProtector   = tokenProtector;
        _logger           = logger;
        _configProvider   = configProvider;
    }

    // ── Start OAuth ──────────────────────────────────────────────────────────

    public async Task<MetaConnectStartResult> StartConnectionAsync(
        Guid tenantId,
        Guid userId,
        string channel,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _configProvider.GetRuntimeConfigAsync(cancellationToken);
        if (settings is null)
            return new MetaConnectStartResult(false, null, null, "Meta OAuth settings are not configured.");

        if (!ChannelScopes.TryGetValue(channel, out var scopes))
            return new MetaConnectStartResult(false, null, null, "Unsupported channel.");

        var normalizedChannel = channel.Trim().ToLowerInvariant();
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        var stateEntity = MetaOAuthState.Create(tenantId, userId, normalizedChannel, state, expiresAt, returnUrl);
        await _dbContext.MetaOAuthStates.AddAsync(stateEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var scopeCsv       = string.Join(',', scopes);
        var callbackUrl    = BuildCallbackUrl(settings);
        var authorizationUrl =
            $"https://www.facebook.com/v{settings.GraphVersion}/dialog/oauth" +
            $"?client_id={Uri.EscapeDataString(settings.AppId)}" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&scope={Uri.EscapeDataString(scopeCsv)}";

        if (!string.IsNullOrWhiteSpace(settings.LoginConfigurationId))
        {
            authorizationUrl += $"&config_id={Uri.EscapeDataString(settings.LoginConfigurationId)}";
        }

        return new MetaConnectStartResult(true, authorizationUrl, state);
    }

    // ── Complete OAuth callback ──────────────────────────────────────────────

    public async Task<MetaConnectCallbackResult> CompleteConnectionAsync(
        string state,
        string code,
        CancellationToken cancellationToken = default)
    {
        var settings = await _configProvider.GetRuntimeConfigAsync(cancellationToken);
        if (settings is null)
            return new MetaConnectCallbackResult(false, null, null, null, "Meta OAuth settings are not configured.");

        var stateEntity = await _dbContext.MetaOAuthStates
            .FirstOrDefaultAsync(x => x.State == state, cancellationToken);

        if (stateEntity is null || !stateEntity.IsUsable(DateTime.UtcNow))
            return new MetaConnectCallbackResult(false, null, null, null, "Invalid or expired OAuth state.");

        stateEntity.MarkConsumed();
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tenantId = stateEntity.TenantId;
        if (!tenantId.HasValue)
            return new MetaConnectCallbackResult(false, null, null, null, "OAuth state is missing tenant context.");

        var normalizedChannel = stateEntity.Channel;

        // Exchange auth code → short-lived token
        var callbackUrl  = BuildCallbackUrl(settings);
        var tokenResult  = await ExchangeCodeForTokenAsync(settings, code, callbackUrl, cancellationToken);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            await LogTokenExchangeFailureAsync(tenantId, normalizedChannel, tokenResult, cancellationToken);
            return new MetaConnectCallbackResult(false, null, null, stateEntity.ReturnUrl,
                tokenResult.ErrorMessage ?? "Token exchange failed.");
        }

        var accessToken = tokenResult.AccessToken;
        var expiresAt   = tokenResult.ExpiresAtUtc;

        // Exchange short-lived → long-lived user token
        var longLived = await ExchangeForLongLivedTokenAsync(settings, accessToken, cancellationToken);
        if (longLived.Success && !string.IsNullOrWhiteSpace(longLived.AccessToken))
        {
            accessToken = longLived.AccessToken;
            expiresAt   = longLived.ExpiresAtUtc ?? expiresAt;
        }

        var encryptedUserToken = _tokenProtector.Protect(accessToken);

        // Upsert MetaChannelConnection (stores the user-level long-lived token)
        var connection = await _dbContext.MetaChannelConnections
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Channel == normalizedChannel, cancellationToken);

        if (connection is null)
        {
            connection = MetaChannelConnection.Create(
                tenantId.Value,
                normalizedChannel,
                encryptedUserToken,
                expiresAt,
                tokenResult.GrantedScopes);
            await _dbContext.MetaChannelConnections.AddAsync(connection, cancellationToken);
        }
        else
        {
            connection.UpdateToken(encryptedUserToken, expiresAt, null, tokenResult.GrantedScopes);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Discover and persist business assets (pages, IG accounts, WABAs, phone numbers)
        await ResolveAndStoreAssetsAsync(
            normalizedChannel, accessToken, connection, tenantId.Value, settings, cancellationToken);

        return new MetaConnectCallbackResult(true, connection.Id, connection.Status, stateEntity.ReturnUrl);
    }

    // ── List connections (with assets) ───────────────────────────────────────

    public async Task<IReadOnlyList<MetaConnectionDto>> ListConnectionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var connections = await _dbContext.MetaChannelConnections
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Channel)
            .ToListAsync(cancellationToken);

        if (connections.Count == 0)
            return [];

        var connectionIds = connections.Select(c => c.Id).ToList();
        var assets = await _dbContext.MetaChannelAssets
            .AsNoTracking()
            .Where(a => connectionIds.Contains(a.ConnectionId))
            .ToListAsync(cancellationToken);

        var assetsByConnection = assets
            .GroupBy(a => a.ConnectionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MetaAssetDto>)g
                .Select(a => new MetaAssetDto(a.Id, a.AssetType, a.ExternalId, a.ExternalName, a.IsActive))
                .ToList());

        return connections.Select(c => new MetaConnectionDto(
            c.Id,
            c.Channel,
            c.Status,
            c.ExternalBusinessId,
            c.ExternalAccountId,
            c.ConnectedAtUtc,
            c.AccessTokenExpiresAtUtc,
            c.LastValidatedAtUtc,
            c.LastError,
            assetsByConnection.GetValueOrDefault(c.Id, [])
        )).ToList();
    }

    // ── Disconnect ───────────────────────────────────────────────────────────

    public async Task<bool> DisconnectAsync(
        Guid tenantId,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.MetaChannelConnections
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == connectionId, cancellationToken);

        if (connection is null)
            return false;

        connection.Revoke("Disconnected by tenant admin.");

        // Deactivate all linked assets
        var assets = await _dbContext.MetaChannelAssets
            .Where(a => a.ConnectionId == connectionId)
            .ToListAsync(cancellationToken);

        foreach (var asset in assets)
            asset.Deactivate();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Asset discovery ──────────────────────────────────────────────────────

    /// <summary>
    /// After OAuth completes, resolves the tenant's business assets from Meta Graph API
    /// and persists them as <see cref="MetaChannelAsset"/> rows.
    /// For Instagram/Facebook: fetches Pages + IG accounts and subscribes webhooks per page.
    /// For WhatsApp: fetches WABAs.
    /// </summary>
    private async Task ResolveAndStoreAssetsAsync(
        string channel,
        string userAccessToken,
        MetaChannelConnection connection,
        Guid tenantId,
        MetaOAuthRuntimeConfig settings,
        CancellationToken ct)
    {
        try
        {
            if (channel is MetaChannelTypes.Instagram or MetaChannelTypes.Facebook)
            {
                await ResolvePageAssetsAsync(channel, userAccessToken, connection, tenantId, settings, ct);
            }
            else if (channel == MetaChannelTypes.WhatsApp)
            {
                await ResolveWabaAssetsAsync(userAccessToken, connection, tenantId, settings, ct);
            }
        }
        catch (Exception ex)
        {
            // Asset discovery failure must not abort the OAuth flow — the connection is already saved.
            _logger.Error(ex, "Asset discovery failed for tenant {TenantId} channel {Channel}", tenantId, channel);
        }
    }

    /// <summary>
    /// Fetches Facebook Pages (with optional linked Instagram Business Accounts),
    /// upserts <see cref="MetaChannelAsset"/> rows, and subscribes webhooks per page.
    /// </summary>
    private async Task ResolvePageAssetsAsync(
        string channel,
        string userAccessToken,
        MetaChannelConnection connection,
        Guid tenantId,
        MetaOAuthRuntimeConfig settings,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);

        // Request page list including Instagram account linkage and page-level token
        var uri = $"v{settings.GraphVersion}/me/accounts" +
                  $"?fields=id,name,access_token,instagram_business_account{{id,name}}" +
                  $"&access_token={Uri.EscapeDataString(userAccessToken)}";

        using var response = await client.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Warning("Failed to fetch pages for tenant {TenantId}: {Status}", tenantId, (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var pagesResponse = JsonSerializer.Deserialize<MetaPagesResponse>(body, CaseInsensitiveOptions);
        if (pagesResponse?.Data is null) return;

        // Track primary IDs to set on the connection
        string? primaryPageId    = null;
        string? primaryIgAccount = null;

        foreach (var page in pagesResponse.Data)
        {
            if (string.IsNullOrWhiteSpace(page.Id)) continue;

            primaryPageId ??= page.Id;

            var encryptedPageToken = !string.IsNullOrWhiteSpace(page.AccessToken)
                ? _tokenProtector.Protect(page.AccessToken)
                : null;

            // Upsert page asset
            await UpsertAssetAsync(tenantId, connection.Id, channel,
                MetaAssetTypes.Page, page.Id, page.Name, encryptedPageToken, ct);

            // Upsert linked Instagram Business Account asset
            if (page.InstagramBusinessAccount is { Id: not null })
            {
                primaryIgAccount ??= page.InstagramBusinessAccount.Id;
                await UpsertAssetAsync(tenantId, connection.Id, MetaChannelTypes.Instagram,
                    MetaAssetTypes.IgAccount,
                    page.InstagramBusinessAccount.Id,
                    page.InstagramBusinessAccount.Name,
                    pageAccessTokenCiphertext: null, ct);
            }

            // Subscribe webhooks for this page (requires page-level token)
            if (!string.IsNullOrWhiteSpace(page.AccessToken))
            {
                await SubscribePageWebhookAsync(page.Id, page.AccessToken, settings.GraphVersion, client, tenantId, ct);
            }
        }

        // Update connection with primary identifiers
        connection.AttachExternalIds(primaryPageId, primaryIgAccount);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Subscribes a Facebook Page to the platform's webhook to receive IG DM / messaging events.
    /// POST /{page-id}/subscribed_apps?subscribed_fields=...&access_token={page_token}
    /// </summary>
    private async Task SubscribePageWebhookAsync(
        string pageId,
        string pageAccessToken,
        string graphVersion,
        HttpClient client,
        Guid tenantId,
        CancellationToken ct)
    {
        try
        {
            var uri = $"v{graphVersion}/{pageId}/subscribed_apps" +
                      $"?subscribed_fields={Uri.EscapeDataString(InstagramPageWebhookFields)}" +
                      $"&access_token={Uri.EscapeDataString(pageAccessToken)}";

            using var resp = await client.PostAsync(uri, content: null, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                _logger.Warning("Webhook subscription failed for page {PageId} tenant {TenantId}: {Error}",
                    pageId, tenantId, err);
            }
            else
            {
                _logger.Info("Webhook subscribed for page {PageId} tenant {TenantId}", pageId, tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception subscribing webhook for page {PageId} tenant {TenantId}", pageId, tenantId);
        }
    }

    /// <summary>
    /// Fetches WhatsApp Business Accounts (WABAs) for the user and upserts WABA assets.
    /// </summary>
    private async Task ResolveWabaAssetsAsync(
        string userAccessToken,
        MetaChannelConnection connection,
        Guid tenantId,
        MetaOAuthRuntimeConfig settings,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);

        var uri = $"v{settings.GraphVersion}/me/whatsapp_business_accounts" +
                  $"?fields=id,name" +
                  $"&access_token={Uri.EscapeDataString(userAccessToken)}";

        using var response = await client.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Warning("Failed to fetch WABAs for tenant {TenantId}: {Status}", tenantId, (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var wabasResponse = JsonSerializer.Deserialize<MetaWabaListResponse>(body, CaseInsensitiveOptions);
        if (wabasResponse?.Data is null) return;

        string? primaryWabaId = null;

        foreach (var waba in wabasResponse.Data)
        {
            if (string.IsNullOrWhiteSpace(waba.Id)) continue;
            primaryWabaId ??= waba.Id;

            await UpsertAssetAsync(tenantId, connection.Id, MetaChannelTypes.WhatsApp,
                MetaAssetTypes.Waba, waba.Id, waba.Name,
                pageAccessTokenCiphertext: null, ct);
        }

        connection.AttachExternalIds(primaryWabaId, externalAccountId: null);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Insert-or-update a <see cref="MetaChannelAsset"/> row.
    /// </summary>
    private async Task UpsertAssetAsync(
        Guid tenantId,
        Guid connectionId,
        string channel,
        string assetType,
        string externalId,
        string? externalName,
        string? pageAccessTokenCiphertext,
        CancellationToken ct)
    {
        // Must bypass tenant filter because the tenant context may not be set during callback
        var existing = await _dbContext.MetaChannelAssets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a =>
                a.TenantId == tenantId &&
                a.ConnectionId == connectionId &&
                a.AssetType == assetType &&
                a.ExternalId == externalId, ct);

        if (existing is null)
        {
            var asset = MetaChannelAsset.Create(
                tenantId, connectionId, channel, assetType, externalId, externalName, pageAccessTokenCiphertext);
            await _dbContext.MetaChannelAssets.AddAsync(asset, ct);
        }
        else
        {
            existing.Update(externalName, pageAccessTokenCiphertext);
        }

        // SaveChanges is batched by the caller to reduce round-trips
    }

    // ── Token exchange helpers ───────────────────────────────────────────────

    private async Task<MetaTokenExchangeResult> ExchangeCodeForTokenAsync(
        MetaOAuthRuntimeConfig settings,
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);
        var uri =
            $"v{settings.GraphVersion}/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(settings.AppId)}" +
            $"&client_secret={Uri.EscapeDataString(settings.AppSecret)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code={Uri.EscapeDataString(code)}";

        using var response = await client.GetAsync(uri, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new MetaTokenExchangeResult(false, ErrorMessage: $"Meta token exchange failed ({(int)response.StatusCode}).", RawBody: content);

        var dto = JsonSerializer.Deserialize<MetaAccessTokenResponse>(content, CaseInsensitiveOptions);
        if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
            return new MetaTokenExchangeResult(false, ErrorMessage: "Meta token response is invalid.", RawBody: content);

        DateTime? expiresAt = dto.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(dto.ExpiresIn) : null;
        return new MetaTokenExchangeResult(true, dto.AccessToken, expiresAt, ParseScopes(dto.GrantedScopes), content);
    }

    private async Task<MetaTokenExchangeResult> ExchangeForLongLivedTokenAsync(
        MetaOAuthRuntimeConfig settings,
        string shortLivedToken,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);
        var uri =
            $"v{settings.GraphVersion}/oauth/access_token" +
            $"?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(settings.AppId)}" +
            $"&client_secret={Uri.EscapeDataString(settings.AppSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(shortLivedToken)}";

        using var response = await client.GetAsync(uri, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new MetaTokenExchangeResult(false, ErrorMessage: "Long-lived token exchange failed.", RawBody: content);

        var dto = JsonSerializer.Deserialize<MetaAccessTokenResponse>(content, CaseInsensitiveOptions);
        if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
            return new MetaTokenExchangeResult(false, ErrorMessage: "Long-lived token response invalid.", RawBody: content);

        DateTime? expiresAt = dto.ExpiresIn > 0 ? DateTime.UtcNow.AddSeconds(dto.ExpiresIn) : null;
        return new MetaTokenExchangeResult(true, dto.AccessToken, expiresAt, ParseScopes(dto.GrantedScopes), content);
    }

    // ── Logging / utility ────────────────────────────────────────────────────

    private async Task LogTokenExchangeFailureAsync(
        Guid? tenantId,
        string channel,
        MetaTokenExchangeResult result,
        CancellationToken ct)
    {
        await _logger.LogIncomingAsync(
            tenantId,
            "meta-oauth",
            "callback-exchange",
            endpoint: "/oauth/access_token",
            requestPayload: JsonSerializer.Serialize(new { channel }),
            isSuccess: false,
            statusCode: 500,
            responsePayload: result.RawBody ?? "token exchange failed",
            errorMessage: result.ErrorMessage,
            cancellationToken: ct);
    }

    private static IReadOnlyList<string> ParseScopes(string? grantedScopes)
    {
        if (string.IsNullOrWhiteSpace(grantedScopes)) return [];
        return grantedScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildCallbackUrl(MetaOAuthRuntimeConfig settings)
    {
        return $"{settings.CallbackBaseUrl.TrimEnd('/')}/api/integrations/meta/callback";
    }

    // ── Private result records ───────────────────────────────────────────────

    private sealed record MetaTokenExchangeResult(
        bool Success,
        string? AccessToken = null,
        DateTime? ExpiresAtUtc = null,
        IReadOnlyList<string>? GrantedScopes = null,
        string? RawBody = null,
        string? ErrorMessage = null);

    // ── Deserialization models ───────────────────────────────────────────────

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class MetaAccessTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")]   public int ExpiresIn { get; set; }
        [JsonPropertyName("granted_scopes")] public string? GrantedScopes { get; set; }
    }

    private sealed class MetaPagesResponse
    {
        public List<MetaPageEntry>? Data { get; set; }
    }

    private sealed class MetaPageEntry
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("instagram_business_account")] public MetaIgAccountRef? InstagramBusinessAccount { get; set; }
    }

    private sealed class MetaIgAccountRef
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class MetaWabaListResponse
    {
        public List<MetaWabaEntry>? Data { get; set; }
    }

    private sealed class MetaWabaEntry
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
