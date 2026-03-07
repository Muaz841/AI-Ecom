using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed record RegisterUserRequest(
    Guid ClientId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "user");

public sealed record LoginRequest(
    Guid ClientId,
    string Email,
    string Password);

public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record RequestPasswordResetRequest(
    Guid ClientId,
    string Email);

public sealed record ResetPasswordRequest(
    Guid ClientId,
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
    string? ErrorMessage = null);
