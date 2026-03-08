using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcomAI.Platform.Infrastructure.ExternalServices;

public sealed class MetaIntegrationService : IMetaIntegrationService
{
    public const string MetaOAuthHttpClientName = "MetaOAuthApi";

    private static readonly Dictionary<string, string[]> ChannelScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        [MetaChannelTypes.Instagram] =
        [
            "instagram_business_basic",
            "instagram_business_manage_messages",
            "instagram_business_manage_comments",
            "pages_show_list",
            "pages_manage_metadata"
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

    private readonly PlatformDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenProtector _tokenProtector;
    private readonly IApplicationLogger _logger;
    private readonly MetaOAuthSettings _settings;

    public MetaIntegrationService(
        PlatformDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ITokenProtector tokenProtector,
        IApplicationLogger logger,
        IOptions<MetaOAuthSettings> settings)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _tokenProtector = tokenProtector;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<MetaConnectStartResult> StartConnectionAsync(
        Guid tenantId,
        Guid userId,
        string channel,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured())
        {
            return new MetaConnectStartResult(false, null, null, "Meta OAuth settings are not configured.");
        }

        if (!ChannelScopes.TryGetValue(channel, out var scopes))
        {
            return new MetaConnectStartResult(false, null, null, "Unsupported channel.");
        }

        var normalizedChannel = channel.Trim().ToLowerInvariant();
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.StateLifetimeMinutes);

        var stateEntity = MetaOAuthState.Create(tenantId, userId, normalizedChannel, state, expiresAt);
        await _dbContext.MetaOAuthStates.AddAsync(stateEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var scopeCsv = string.Join(',', scopes);
        var callbackUrl = BuildCallbackUrl(normalizedChannel, returnUrl);
        var authorizationUrl =
            $"https://www.facebook.com/v{_settings.GraphVersion}/dialog/oauth" +
            $"?client_id={Uri.EscapeDataString(_settings.AppId)}" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&scope={Uri.EscapeDataString(scopeCsv)}";

        return new MetaConnectStartResult(true, authorizationUrl, state);
    }

    public async Task<MetaConnectCallbackResult> CompleteConnectionAsync(
        string channel,
        string state,
        string code,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured())
        {
            return new MetaConnectCallbackResult(false, null, null, "Meta OAuth settings are not configured.");
        }

        var normalizedChannel = channel.Trim().ToLowerInvariant();
        var stateEntity = await _dbContext.MetaOAuthStates
            .FirstOrDefaultAsync(x => x.State == state && x.Channel == normalizedChannel, cancellationToken);

        if (stateEntity is null || !stateEntity.IsUsable(DateTime.UtcNow))
        {
            return new MetaConnectCallbackResult(false, null, null, "Invalid or expired OAuth state.");
        }

        stateEntity.MarkConsumed();
        await _dbContext.SaveChangesAsync(cancellationToken);
        var tenantId = stateEntity.TenantId ?? stateEntity.ClientId;

        var callbackUrl = BuildCallbackUrl(normalizedChannel, returnUrl);
        var tokenResult = await ExchangeCodeForTokenAsync(code, callbackUrl, cancellationToken);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            await _logger.LogIncomingAsync(
                tenantId,
                "meta-oauth",
                "callback-exchange",
                endpoint: "/oauth/access_token",
                requestPayload: JsonSerializer.Serialize(new { channel = normalizedChannel }),
                isSuccess: false,
                statusCode: 500,
                responsePayload: tokenResult.RawBody ?? "token exchange failed",
                errorMessage: tokenResult.ErrorMessage,
                cancellationToken: cancellationToken);
            return new MetaConnectCallbackResult(false, null, null, tokenResult.ErrorMessage ?? "Token exchange failed.");
        }

        var accessToken = tokenResult.AccessToken;
        var expiresAt = tokenResult.ExpiresAtUtc;

        var longLived = await ExchangeForLongLivedTokenAsync(accessToken, cancellationToken);
        if (longLived.Success && !string.IsNullOrWhiteSpace(longLived.AccessToken))
        {
            accessToken = longLived.AccessToken;
            expiresAt = longLived.ExpiresAtUtc ?? expiresAt;
        }

        var meInfo = await FetchCurrentUserAsync(accessToken, cancellationToken);
        var encryptedAccessToken = _tokenProtector.Protect(accessToken);

        var connection = await _dbContext.MetaChannelConnections
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Channel == normalizedChannel, cancellationToken);

        if (connection is null)
        {
            connection = MetaChannelConnection.Create(
                tenantId,
                normalizedChannel,
                encryptedAccessToken,
                expiresAt,
                tokenResult.GrantedScopes);
            connection.AttachExternalIds(meInfo.ExternalBusinessId, meInfo.ExternalAccountId);
            await _dbContext.MetaChannelConnections.AddAsync(connection, cancellationToken);
        }
        else
        {
            connection.UpdateToken(encryptedAccessToken, expiresAt, null, tokenResult.GrantedScopes);
            connection.AttachExternalIds(meInfo.ExternalBusinessId, meInfo.ExternalAccountId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MetaConnectCallbackResult(true, connection.Id, connection.Status);
    }

    public async Task<IReadOnlyList<MetaConnectionDto>> ListConnectionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.MetaChannelConnections
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Channel)
            .Select(x => new MetaConnectionDto(
                x.Id,
                x.Channel,
                x.Status,
                x.ExternalBusinessId,
                x.ExternalAccountId,
                x.ConnectedAtUtc,
                x.AccessTokenExpiresAtUtc,
                x.LastValidatedAtUtc,
                x.LastError))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<bool> DisconnectAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _dbContext.MetaChannelConnections
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == connectionId, cancellationToken);

        if (connection is null)
        {
            return false;
        }

        connection.Revoke("Disconnected by tenant admin.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<MetaTokenExchangeResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);
        var uri =
            $"v{_settings.GraphVersion}/oauth/access_token" +
            $"?client_id={Uri.EscapeDataString(_settings.AppId)}" +
            $"&client_secret={Uri.EscapeDataString(_settings.AppSecret)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code={Uri.EscapeDataString(code)}";

        using var response = await client.GetAsync(uri, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new MetaTokenExchangeResult(false, ErrorMessage: $"Meta token exchange failed ({(int)response.StatusCode}).", RawBody: content);
        }

        var dto = JsonSerializer.Deserialize<MetaAccessTokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
        {
            return new MetaTokenExchangeResult(false, ErrorMessage: "Meta token response is invalid.", RawBody: content);
        }

        DateTime? expiresAt = null;
        if (dto.ExpiresIn > 0)
        {
            expiresAt = DateTime.UtcNow.AddSeconds(dto.ExpiresIn);
        }

        var grantedScopes = ParseScopes(dto.GrantedScopes);
        return new MetaTokenExchangeResult(true, dto.AccessToken, expiresAt, grantedScopes, content);
    }

    private async Task<MetaTokenExchangeResult> ExchangeForLongLivedTokenAsync(string shortLivedToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);
        var uri =
            $"v{_settings.GraphVersion}/oauth/access_token" +
            $"?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(_settings.AppId)}" +
            $"&client_secret={Uri.EscapeDataString(_settings.AppSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(shortLivedToken)}";

        using var response = await client.GetAsync(uri, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new MetaTokenExchangeResult(false, ErrorMessage: "Long-lived token exchange failed.", RawBody: content);
        }

        var dto = JsonSerializer.Deserialize<MetaAccessTokenResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
        {
            return new MetaTokenExchangeResult(false, ErrorMessage: "Long-lived token response invalid.", RawBody: content);
        }

        DateTime? expiresAt = null;
        if (dto.ExpiresIn > 0)
        {
            expiresAt = DateTime.UtcNow.AddSeconds(dto.ExpiresIn);
        }

        return new MetaTokenExchangeResult(true, dto.AccessToken, expiresAt, ParseScopes(dto.GrantedScopes), content);
    }

    private async Task<MetaGraphMeResult> FetchCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(MetaOAuthHttpClientName);
        var uri =
            $"v{_settings.GraphVersion}/me?fields=id,name" +
            $"&access_token={Uri.EscapeDataString(accessToken)}";

        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new MetaGraphMeResult(null, null);
        }

        var dto = await response.Content.ReadFromJsonAsync<MetaMeResponse>(cancellationToken: cancellationToken);
        return new MetaGraphMeResult(dto?.Id, dto?.Id);
    }

    private static IReadOnlyList<string> ParseScopes(string? grantedScopes)
    {
        if (string.IsNullOrWhiteSpace(grantedScopes))
        {
            return [];
        }

        return grantedScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string BuildCallbackUrl(string channel, string? returnUrl)
    {
        var callback = $"{_settings.CallbackBaseUrl.TrimEnd('/')}/api/integrations/meta/callback";
        var query = $"channel={Uri.EscapeDataString(channel)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            query += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return $"{callback}?{query}";
    }

    private sealed record MetaTokenExchangeResult(
        bool Success,
        string? AccessToken = null,
        DateTime? ExpiresAtUtc = null,
        IReadOnlyList<string>? GrantedScopes = null,
        string? RawBody = null,
        string? ErrorMessage = null);

    private sealed record MetaGraphMeResult(string? ExternalBusinessId, string? ExternalAccountId);

    private sealed class MetaAccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("granted_scopes")]
        public string? GrantedScopes { get; set; }
    }

    private sealed class MetaMeResponse
    {
        public string? Id { get; set; }
    }
}

public sealed class MetaOAuthSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string CallbackBaseUrl { get; set; } = string.Empty;
    public string GraphVersion { get; set; } = "20.0";
    public int StateLifetimeMinutes { get; set; } = 10;

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(AppId)
            && !string.IsNullOrWhiteSpace(AppSecret)
            && !string.IsNullOrWhiteSpace(CallbackBaseUrl);
    }
}
