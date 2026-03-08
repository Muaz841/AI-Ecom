using System;
using System.Collections.Generic;

namespace EcomAI.Platform.Business.Entities;

public class UserAccount : Entity<Guid>, ITenantEntity
{
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Role { get; private set; } = "user";
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public List<UserRefreshToken> RefreshTokens { get; private set; } = new();
    public List<UserPasswordResetToken> PasswordResetTokens { get; private set; } = new();
    public List<UserRole> UserRoles { get; private set; } = new();

    private UserAccount()
    {
    }

    public static UserAccount Create(
        Guid tenantId,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string role = "user")
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("FirstName is required.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("LastName is required.", nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role is required.", nameof(role));
        }

        return new UserAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim(),
            NormalizedEmail = email.Trim().ToUpperInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Role = role.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    public void MarkLogin(DateTime loginAtUtc)
    {
        LastLoginAt = loginAtUtc;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}

public class UserRefreshToken : Entity<Guid>, ITenantEntity
{
    public Guid UserAccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }

    private UserRefreshToken()
    {
    }

    public static UserRefreshToken Create(Guid tenantId, Guid userAccountId, string tokenHash, DateTime expiresAtUtc)
    {
        if (tenantId == Guid.Empty || userAccountId == Guid.Empty)
        {
            throw new ArgumentException("TenantId and UserAccountId are required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("Refresh token expiry must be in the future.", nameof(expiresAtUtc));
        }

        return new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserAccountId = userAccountId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAtUtc,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;

    public void Revoke(string reason)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? "revoked" : reason.Trim();
    }
}

public class UserPasswordResetToken : Entity<Guid>, ITenantEntity
{
    public Guid UserAccountId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    private UserPasswordResetToken()
    {
    }

    public static UserPasswordResetToken Create(Guid tenantId, Guid userAccountId, string tokenHash, DateTime expiresAtUtc)
    {
        if (tenantId == Guid.Empty || userAccountId == Guid.Empty)
        {
            throw new ArgumentException("TenantId and UserAccountId are required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new ArgumentException("Reset token expiry must be in the future.", nameof(expiresAtUtc));
        }

        return new UserPasswordResetToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserAccountId = userAccountId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAtUtc,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsUsable(DateTime utcNow) => UsedAt is null && ExpiresAt > utcNow;

    public void MarkUsed()
    {
        UsedAt = DateTime.UtcNow;
    }
}


