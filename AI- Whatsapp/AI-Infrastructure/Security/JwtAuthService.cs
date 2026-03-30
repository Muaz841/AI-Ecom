using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class JwtAuthService : IAuthService
{
    private readonly PlatformDbContext _dbContext;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly IApplicationLogger _logger;
    private readonly IEmailService _emailService;
    private readonly JwtAuthSettings _settings;

    // Maximum OTP requests allowed per user within the lookback window.
    private const int OtpRateLimitMax = 3;
    private const int OtpRateLimitWindowMinutes = 60;

    public JwtAuthService(
        PlatformDbContext dbContext,
        IPasswordHasher<UserAccount> passwordHasher,
        IApplicationLogger logger,
        IEmailService emailService,
        IOptions<JwtAuthSettings> settings)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _emailService = emailService;
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.SigningKey) || _settings.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Authentication:Jwt:SigningKey must be at least 32 characters.");
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var normalizedEmail = NormalizeEmail(request.Email);
        var exists = await _dbContext.UserAccounts
            .AnyAsync(x => x.TenantId == request.TenantId && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
        {
            return new AuthResult(false, ErrorMessage: "User already exists for this client.");
        }

        var temporary = UserAccount.Create(
            request.TenantId,
            request.Email.Trim(),
            "placeholder",
            request.FirstName,
            request.LastName,
            request.Role);

        var hashedPassword = _passwordHasher.HashPassword(temporary, request.Password);
        temporary.SetPasswordHash(hashedPassword);

        await _dbContext.UserAccounts.AddAsync(temporary, cancellationToken);

        var requestedRoleCode = (request.Role ?? "user").Trim().ToLowerInvariant();
        var role = await _dbContext.Set<Role>()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Code == requestedRoleCode, cancellationToken);

        if (role is not null)
        {
            await _dbContext.Set<UserRole>().AddAsync(UserRole.Create(request.TenantId, temporary.Id, role.Id), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(temporary, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResult(false, ErrorMessage: "Invalid login request.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);

        // TenantId = null → host login. Only users with TenantId = null are host users.
        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new AuthResult(false, ErrorMessage: "Invalid credentials.");
        }

        // Security: if this is a host-mode login (TenantId = null), the matched user MUST be a host user.
        // This is guaranteed by the query above, but explicitly block tenant users trying the host endpoint.
        if (request.TenantId is null && user.TenantId is not null)
        {
            return new AuthResult(false, ErrorMessage: "Host access is restricted to platform administrators.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verifyResult is PasswordVerificationResult.Failed)
        {
            return new AuthResult(false, ErrorMessage: "Invalid credentials.");
        }

        user.MarkLogin(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new AuthResult(false, ErrorMessage: "Refresh token is required.");
        }

        var tokenHash = ComputeSha256(request.RefreshToken.Trim());
        var stored = await _dbContext.UserRefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (stored is null || !stored.IsActive(DateTime.UtcNow))
        {
            return new AuthResult(false, ErrorMessage: "Refresh token is invalid.");
        }

        var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == stored.UserAccountId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return new AuthResult(false, ErrorMessage: "User is not active.");
        }

        stored.Revoke("rotated");
        var result = await IssueTokensAsync(user, cancellationToken);
        return result;
    }

    public async Task<AuthProfileResult?> GetProfileAsync(Guid? tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && x.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roleCodes = await (
            from userRole in _dbContext.Set<UserRole>()
            join role in _dbContext.Set<Role>() on userRole.RoleId equals role.Id
            where userRole.UserAccountId == user.Id && userRole.TenantId == tenantId
            select role.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissionCodes = await (
            from userRole in _dbContext.Set<UserRole>()
            join rolePermission in _dbContext.Set<RolePermission>() on userRole.RoleId equals rolePermission.RoleId
            join permission in _dbContext.Set<Permission>() on rolePermission.PermissionId equals permission.Id
            where userRole.UserAccountId == user.Id && userRole.TenantId == tenantId
            select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new AuthProfileResult(
            user.Id,
            tenantId,
            user.Email,
            user.FirstName,
            user.LastName,
            roleCodes,
            permissionCodes,
            IsHost: tenantId is null);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = ComputeSha256(request.RefreshToken.Trim());
        var stored = await _dbContext.UserRefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            return;
        }

        stored.Revoke("logout");
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email))
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // Silently return — never reveal whether the email exists.
            return;
        }

        // Rate-limit: max OtpRateLimitMax requests per user within the lookback window.
        var windowStart = DateTime.UtcNow.AddMinutes(-OtpRateLimitWindowMinutes);
        var recentCount = await _dbContext.UserPasswordResetTokens
            .CountAsync(x => x.UserAccountId == user.Id && x.CreatedAt > windowStart, cancellationToken);

        if (recentCount >= OtpRateLimitMax)
        {
            _logger.Warning(
                "OTP rate limit exceeded for user {UserId} — {Count} requests in last {Window} minutes.",
                user.Id, recentCount, OtpRateLimitWindowMinutes);
            return;
        }

        // Generate a 6-digit cryptographically random OTP.
        // Salt the hash with the user's ID to prevent cross-user hash collisions in the
        // unique TokenHash index (two users could otherwise produce the same 6-digit OTP).
        var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        var otpHash = ComputeSha256($"{user.Id}:{otp}");
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.OtpLifetimeMinutes);

        var otpEntity = UserPasswordResetToken.Create(user.TenantId, user.Id, otpHash, expiresAt);
        await _dbContext.UserPasswordResetTokens.AddAsync(otpEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendOtpEmailAsync(
            user.Email,
            $"{user.FirstName} {user.LastName}".Trim(),
            otp,
            _settings.OtpLifetimeMinutes,
            cancellationToken);

        _logger.Info(
            "OTP issued and emailed for user {UserId}, tenant {TenantId}, expires {ExpiresAt}.",
            user.Id, user.TenantId, expiresAt);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Otp))
        {
            return new VerifyOtpResult(false, ErrorMessage: "Invalid request.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new VerifyOtpResult(false, ErrorMessage: "Invalid OTP or email.");
        }

        var otpHash = ComputeSha256($"{user.Id}:{request.Otp.Trim()}");
        var stored = await _dbContext.UserPasswordResetTokens
            .Where(x => x.UserAccountId == user.Id && x.TokenHash == otpHash)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null || !stored.IsUsable(DateTime.UtcNow))
        {
            _logger.Warning("OTP verification failed for user {UserId} — token invalid or expired.", user.Id);
            return new VerifyOtpResult(false, ErrorMessage: "OTP is invalid or has expired.");
        }

        // Consume the OTP — it is single-use.
        stored.MarkUsed();

        // Issue a short-lived reset token the client will use in the final reset step.
        var resetToken = GenerateSecureToken();
        var resetHash = ComputeSha256(resetToken);
        var resetEntity = UserPasswordResetToken.Create(
            user.TenantId,
            user.Id,
            resetHash,
            DateTime.UtcNow.AddMinutes(_settings.PasswordResetTokenLifetimeMinutes));

        await _dbContext.UserPasswordResetTokens.AddAsync(resetEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.Info("OTP verified for user {UserId}; reset token issued.", user.Id);

        return new VerifyOtpResult(true, ResetToken: resetToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.ResetToken) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new InvalidOperationException("Invalid reset password request.");
        }

        if (request.NewPassword.Length < 8)
        {
            throw new InvalidOperationException("Password must contain at least 8 characters.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new InvalidOperationException("User not found.");
        }

        var tokenHash = ComputeSha256(request.ResetToken.Trim());
        var stored = await _dbContext.UserPasswordResetTokens
            .Where(x => x.UserAccountId == user.Id && x.TokenHash == tokenHash)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null || !stored.IsUsable(DateTime.UtcNow))
        {
            throw new InvalidOperationException("Reset token is invalid or expired.");
        }

        stored.MarkUsed();
        user.SetPasswordHash(_passwordHasher.HashPassword(user, request.NewPassword));

        var activeRefreshTokens = await _dbContext.UserRefreshTokens
            .Where(x => x.UserAccountId == user.Id && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeRefreshTokens)
        {
            token.Revoke("password-reset");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResult> IssueTokensAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessExpiresAt = now.AddMinutes(_settings.AccessTokenLifetimeMinutes);
        var refreshExpiresAt = now.AddDays(_settings.RefreshTokenLifetimeDays);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tenantId = user.TenantId; // null for host users

        string? tenantBusinessName = null;
        if (tenantId.HasValue)
        {
            tenantBusinessName = await _dbContext.Tenants
                .Where(t => t.Id == tenantId.Value)
                .Select(t => t.BusinessName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var roleCodes = await (
            from userRole in _dbContext.Set<UserRole>()
            join role in _dbContext.Set<Role>() on userRole.RoleId equals role.Id
            where userRole.UserAccountId == user.Id && userRole.TenantId == tenantId
            select role.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissionCodes = await (
            from userRole in _dbContext.Set<UserRole>()
            join rolePermission in _dbContext.Set<RolePermission>() on userRole.RoleId equals rolePermission.RoleId
            join permission in _dbContext.Set<Permission>() on rolePermission.PermissionId equals permission.Id
            where userRole.UserAccountId == user.Id && userRole.TenantId == tenantId
            select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("tenant_id", tenantId?.ToString() ?? string.Empty),
            new("tenant_name", tenantBusinessName ?? string.Empty),
            new("is_host", tenantId is null ? "true" : "false"),
        };

        claims.AddRange(roleCodes.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissionCodes.Select(code => new Claim("permission", code)));

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpiresAt,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        var refreshToken = GenerateSecureToken();
        var refreshHash = ComputeSha256(refreshToken);

        var refreshEntity = UserRefreshToken.Create(tenantId, user.Id, refreshHash, refreshExpiresAt);
        await _dbContext.UserRefreshTokens.AddAsync(refreshEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResult(
            Success: true,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiresAtUtc: accessExpiresAt,
            UserId: user.Id,
            Email: user.Email,
            Tenantname: tenantBusinessName,
            IsHost: tenantId is null,
            Role: roleCodes.FirstOrDefault() ?? user.Role);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private void ValidateRequest(RegisterUserRequest request)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            throw new InvalidOperationException("Valid email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new InvalidOperationException("Password must contain at least 8 characters.");
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeSha256(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }
}

public sealed class JwtAuthSettings
{
    public string Issuer { get; set; } = "EcomAI";
    public string Audience { get; set; } = "EcomAI.Clients";
    public string SigningKey { get; set; } = "CHANGE_ME_AT_LEAST_32_CHARS_LONG_SIGNING_KEY";
    public int AccessTokenLifetimeMinutes { get; set; } = 30;
    public int RefreshTokenLifetimeDays { get; set; } = 14;
    /// <summary>Lifetime of the verified reset token issued after OTP confirmation.</summary>
    public int PasswordResetTokenLifetimeMinutes { get; set; } = 30;
    /// <summary>Lifetime of the OTP code sent to the user's email.</summary>
    public int OtpLifetimeMinutes { get; set; } = 10;
}
