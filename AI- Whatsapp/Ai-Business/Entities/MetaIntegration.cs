using System;
using System.Collections.Generic;
using System.Linq;

namespace EcomAI.Platform.Business.Entities;

public static class MetaChannelTypes
{
    public const string Instagram = "instagram";
    public const string Facebook = "facebook";
    public const string WhatsApp = "whatsapp";

    public static readonly string[] All = [Instagram, Facebook, WhatsApp];
}

public static class MetaConnectionStatuses
{
    public const string Active = "active";
    public const string Revoked = "revoked";
    public const string Error = "error";
}

public sealed class MetaChannelConnection : Entity<Guid>, ITenantEntity
{
    public Guid ClientId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Status { get; private set; } = MetaConnectionStatuses.Active;
    public string? ExternalBusinessId { get; private set; }
    public string? ExternalAccountId { get; private set; }
    public string AccessTokenCiphertext { get; private set; } = null!;
    public string? RefreshTokenCiphertext { get; private set; }
    public DateTime? AccessTokenExpiresAtUtc { get; private set; }
    public string? ScopesCsv { get; private set; }
    public DateTime ConnectedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? LastValidatedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private MetaChannelConnection()
    {
    }

    public static MetaChannelConnection Create(
        Guid tenantId,
        string channel,
        string accessTokenCiphertext,
        DateTime? accessTokenExpiresAtUtc,
        IEnumerable<string>? scopes = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (!MetaChannelTypes.All.Contains(channel, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid channel.", nameof(channel));
        }

        if (string.IsNullOrWhiteSpace(accessTokenCiphertext))
        {
            throw new ArgumentException("Encrypted access token is required.", nameof(accessTokenCiphertext));
        }

        var now = DateTime.UtcNow;
        return new MetaChannelConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientId = tenantId,
            Channel = channel.Trim().ToLowerInvariant(),
            AccessTokenCiphertext = accessTokenCiphertext,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            ScopesCsv = NormalizeScopes(scopes),
            Status = MetaConnectionStatuses.Active,
            ConnectedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void UpdateToken(
        string accessTokenCiphertext,
        DateTime? accessTokenExpiresAtUtc,
        string? refreshTokenCiphertext = null,
        IEnumerable<string>? scopes = null)
    {
        if (string.IsNullOrWhiteSpace(accessTokenCiphertext))
        {
            throw new ArgumentException("Encrypted access token is required.", nameof(accessTokenCiphertext));
        }

        AccessTokenCiphertext = accessTokenCiphertext;
        RefreshTokenCiphertext = string.IsNullOrWhiteSpace(refreshTokenCiphertext)
            ? null
            : refreshTokenCiphertext;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
        ScopesCsv = NormalizeScopes(scopes);
        Status = MetaConnectionStatuses.Active;
        LastError = null;
        LastValidatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AttachExternalIds(string? externalBusinessId, string? externalAccountId)
    {
        ExternalBusinessId = string.IsNullOrWhiteSpace(externalBusinessId) ? null : externalBusinessId.Trim();
        ExternalAccountId = string.IsNullOrWhiteSpace(externalAccountId) ? null : externalAccountId.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkError(string error)
    {
        Status = MetaConnectionStatuses.Error;
        LastError = error;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke(string? reason = null)
    {
        Status = MetaConnectionStatuses.Revoked;
        LastError = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? NormalizeScopes(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return null;
        }

        var normalized = scopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }
}

public sealed class MetaOAuthState : Entity<Guid>, ITenantEntity
{
    public Guid ClientId { get; private set; }
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string State { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

    private MetaOAuthState()
    {
    }

    public static MetaOAuthState Create(Guid tenantId, Guid userId, string channel, string state, DateTime expiresAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (!MetaChannelTypes.All.Contains(channel, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid channel.", nameof(channel));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("State is required.", nameof(state));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("State expiry must be in the future.", nameof(expiresAtUtc));
        }

        return new MetaOAuthState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClientId = tenantId,
            UserId = userId,
            Channel = channel.Trim().ToLowerInvariant(),
            State = state.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public bool IsUsable(DateTime utcNow) => ConsumedAtUtc == null && ExpiresAtUtc > utcNow;

    public void MarkConsumed()
    {
        ConsumedAtUtc = DateTime.UtcNow;
    }
}
