using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRepository<Tenant> _tenantRepository;

    public AuthController(IAuthService authService, IRepository<Tenant> tenantRepository)
    {
        _authService = authService;
        _tenantRepository = tenantRepository;
    }

    [Authorize(Policy = PermissionCodes.UsersManage)]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _authService.RegisterAsync(
                new RegisterUserRequest(
                    request.TenantId,
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
        Guid? resolvedTenantId = null;
        
        var isHostLogin = string.Equals(request.TenantName?.Trim(), "host", StringComparison.OrdinalIgnoreCase);

        if (!isHostLogin)
        {
            if (request.TenantId != Guid.Empty)
            {
                resolvedTenantId = request.TenantId;
            }
            else if (!string.IsNullOrWhiteSpace(request.TenantName))
            {
                var tenantName = request.TenantName.Trim();
                var tenant = await _tenantRepository.FirstOrDefaultAsync(
                    x => x.Name.ToLower() == tenantName.ToLower() || x.BusinessName.ToLower() == tenantName.ToLower());

                if (tenant is null)
                {
                    return Unauthorized("Tenant not found.");
                }

                resolvedTenantId = tenant.Id;
            }
            else
            {
                return BadRequest("TenantId or tenantName is required.");
            }
        }        

        var result = await _authService.LoginAsync(
            new LoginRequest(resolvedTenantId, request.Email, request.Password),
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
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userClaim, out var userId))
        {
            return Unauthorized("Invalid auth claims.");
        }

        var tenantClaim = User.FindFirstValue("tenant_id");
        Guid? tenantId = Guid.TryParse(tenantClaim, out var parsedTenant) ? parsedTenant : null;

        var profile = await _authService.GetProfileAsync(tenantId, userId, cancellationToken);
        if (profile is null)
        {
            return NotFound("User profile not found.");
        }

        return Ok(profile);
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
        var tenantId = await ResolveTenantIdAsync(request.TenantId, request.TenantName, cancellationToken);

        if (tenantId.HasValue)
        {
            await _authService.RequestPasswordResetAsync(
                new RequestPasswordResetRequest(tenantId.Value, request.Email),
                cancellationToken);
        }

        // Always return the same response — never reveal whether the tenant or email exists.
        return Ok(new { message = "If the email is registered, a reset code has been sent." });
    }

    [AllowAnonymous]
    [HttpPost("password/verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpApiRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantIdAsync(request.TenantId, request.TenantName, cancellationToken);

        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "OTP is invalid or has expired." });
        }

        var result = await _authService.VerifyOtpAsync(
            new VerifyOtpRequest(tenantId.Value, request.Email, request.Otp),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(new { resetToken = result.ResetToken });
    }

    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await ResolveTenantIdAsync(request.TenantId, request.TenantName, cancellationToken);

        if (!tenantId.HasValue)
        {
            return BadRequest("Reset token is invalid or expired.");
        }

        try
        {
            await _authService.ResetPasswordAsync(
                new ResetPasswordRequest(tenantId.Value, request.Email, request.ResetToken, request.NewPassword),
                cancellationToken);

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Resolves a tenant GUID from either an explicit ID or a name/businessName lookup.
    /// Returns null if neither can be resolved (caller decides how to handle silently).
    /// </summary>
    private async Task<Guid?> ResolveTenantIdAsync(Guid tenantId, string? tenantName, CancellationToken cancellationToken)
    {
        if (tenantId != Guid.Empty) return tenantId;

        if (string.IsNullOrWhiteSpace(tenantName)) return null;

        var name = tenantName.Trim();
        var tenant = await _tenantRepository.FirstOrDefaultAsync(
            x => x.Name.ToLower() == name.ToLower() || x.BusinessName.ToLower() == name.ToLower());

        return tenant?.Id;
    }
}

public sealed record RegisterRequest(
    Guid TenantId,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role = "user");

public sealed record LoginApiRequest(
    Guid TenantId,
    string? TenantName,
    string Email,
    string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(
    string Email,
    string? TenantName = null,
    Guid TenantId = default);

public sealed record VerifyOtpApiRequest(
    string Email,
    string Otp,
    string? TenantName = null,
    Guid TenantId = default);

public sealed record ResetPasswordApiRequest(
    string Email,
    string ResetToken,
    string NewPassword,
    string? TenantName = null,
    Guid TenantId = default);
