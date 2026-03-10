using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<AuthProfileResult?> GetProfileAsync(Guid TenantId, Guid userId, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed record RegisterUserRequest(
    Guid TenantId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "user");

public sealed record LoginRequest(    
    Guid TenantId,
    string Email,
    string Password);

public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record RequestPasswordResetRequest(
    Guid TenantId,
    string Email);

public sealed record ResetPasswordRequest(
    Guid TenantId,
    string Email,
    string ResetToken,
    string NewPassword);

public sealed record AuthResult(
    bool Success,
    string? AccessToken = null,
    string? RefreshToken = null,
    DateTime? AccessTokenExpiresAtUtc = null,
    Guid? UserId = null,
    string? Email = null,
    string? Role = null,
    string? Tenantname = null,
    string? ErrorMessage = null);

public sealed record AuthProfileResult(
    Guid UserId,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

