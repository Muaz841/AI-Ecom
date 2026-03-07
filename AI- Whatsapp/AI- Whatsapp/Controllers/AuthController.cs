using System;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RegisterAsync(
                new RegisterUserRequest(
                    request.ClientId,
                    request.Email,
                    request.Password,
                    request.FirstName,
                    request.LastName,
                    request.Role),
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginApiRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            new LoginRequest(request.ClientId, request.Email, request.Password),
            cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(result.ErrorMessage);
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(new RefreshTokenRequest(request.RefreshToken), cancellationToken);
        if (!result.Success)
        {
            return Unauthorized(result.ErrorMessage);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(new LogoutRequest(request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.RequestPasswordResetAsync(
            new RequestPasswordResetRequest(request.ClientId, request.Email),
            cancellationToken);

        return Ok(new { message = "If user exists, reset instructions were generated." });
    }

    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _authService.ResetPasswordAsync(
                new ResetPasswordRequest(request.ClientId, request.Email, request.ResetToken, request.NewPassword),
                cancellationToken);

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public sealed record RegisterRequest(
    Guid ClientId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "user");

public sealed record LoginApiRequest(
    Guid ClientId,
    string Email,
    string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(
    Guid ClientId,
    string Email);

public sealed record ResetPasswordApiRequest(
    Guid ClientId,
    string Email,
    string ResetToken,
    string NewPassword);
