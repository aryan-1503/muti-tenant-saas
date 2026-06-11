using InventoryManager.Application.Features.Auth.AcceptInvite;
using InventoryManager.Application.Features.Auth.ForgotPassword;
using InventoryManager.Application.Features.Auth.InviteUser;
using InventoryManager.Application.Features.Auth.Login;
using InventoryManager.Application.Features.Auth.RefreshToken;
using InventoryManager.Application.Features.Auth.Register;
using InventoryManager.Application.Features.Auth.ResetPassword;
using InventoryManager.Application.Features.Auth.RevokeToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Handles all authentication-related HTTP requests.
///
/// REFRESH TOKEN STRATEGY:
/// - Access token  → returned in JSON body (short-lived, 15 min)
/// - Refresh token → stored in HttpOnly, Secure, SameSite=Strict cookie
///
/// HttpOnly means JavaScript CANNOT read the refresh token,
/// protecting it from XSS attacks. The browser sends it automatically
/// with every request to /api/auth/refresh.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private const string RefreshTokenCookieName = "refresh_token";

    public AuthController(IMediator mediator) => _mediator = mediator;

    // ─── POST /api/auth/register ──────────────────────────────────────────────
    /// <summary>Create a new organisation and admin account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthClientResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(ToClientResponse(result));
    }

    // ─── POST /api/auth/login ─────────────────────────────────────────────────
    /// <summary>Authenticate with email and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthClientResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(ToClientResponse(result));
    }

    // ─── POST /api/auth/refresh ───────────────────────────────────────────────
    /// <summary>Exchange the HttpOnly refresh token cookie for a new access token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthClientResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(new { message = "Refresh token not found." });

        var result = await _mediator.Send(new RefreshTokenCommand(refreshToken), ct);
        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(ToClientResponse(result));
    }

    // ─── POST /api/auth/logout ────────────────────────────────────────────────
    /// <summary>Revoke the current refresh token and clear the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _mediator.Send(new RevokeTokenCommand(refreshToken), ct);

        // Clear the cookie on the client side
        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }

    // ─── POST /api/auth/invite ────────────────────────────────────────────────
    /// <summary>Admin/Manager sends a team member invitation email.</summary>
    [HttpPost("invite")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserCommand command, CancellationToken ct)
    {
        var inviteId = await _mediator.Send(command, ct);
        return Ok(new { message = "Invitation sent successfully.", inviteId });
    }

    // ─── POST /api/auth/accept-invite ────────────────────────────────────────
    /// <summary>Accept an invitation, set password, and log in.</summary>
    [HttpPost("accept-invite")]
    [ProducesResponseType(typeof(AuthClientResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        SetRefreshTokenCookie(result.RefreshToken);
        return Ok(ToClientResponse(result));
    }

    // ─── POST /api/auth/forgot-password ──────────────────────────────────────
    /// <summary>Request a password reset email. Always returns 200 (no enumeration).</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "If an account exists for this email, a reset link has been sent." });
    }

    // ─── POST /api/auth/reset-password ───────────────────────────────────────
    /// <summary>Reset password using the emailed token.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "Password reset successfully. Please log in with your new password." });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Sets the refresh token as an HttpOnly, Secure, SameSite=Strict cookie.</summary>
    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,     // JS cannot read this
            Secure = true,       // HTTPS only (set to false for local HTTP dev if needed)
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    /// <summary>
    /// Maps AuthResponse to a client-safe version — RefreshToken is NOT included.
    /// The client only ever sees the access token in JSON.
    /// </summary>
    private static AuthClientResponse ToClientResponse(
        Application.Common.Models.AuthResponse r) =>
        new(r.AccessToken, r.AccessTokenExpiry, r.User);
}

/// <summary>What the client actually receives in the JSON body.</summary>
public record AuthClientResponse(
    string AccessToken,
    DateTime AccessTokenExpiry,
    Application.Common.Models.UserDto User
);
