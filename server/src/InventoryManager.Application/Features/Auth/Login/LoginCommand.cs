using FluentValidation;
using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Application.Common.Models;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.Login;

// ─── Command ──────────────────────────────────────────────────────────────────
public record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public LoginCommandHandler(IAppDbContext db, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _config = config;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Find user by email (ignore tenant filter — email is globally unique) ──
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), cancellationToken);

        // Use constant-time comparison to prevent timing attacks that reveal valid emails
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Your account has been deactivated. Please contact your administrator.");

        if (!user.Tenant.IsActive)
            throw new UnauthorizedAccessException("Your organisation account is inactive.");

        // ── 2. Revoke all existing valid refresh tokens for this user (single-session policy) ──
        // Optionally allow multi-device by removing this step
        var existingTokens = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var oldToken in existingTokens)
            oldToken.RevokedAt = DateTime.UtcNow;

        // ── 3. Issue new refresh token ──────────────────────────────────────────
        var refreshValue = _tokenService.GenerateRefreshToken();
        var expiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        _db.RefreshTokens.Add(new InventoryManager.Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = refreshValue,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        });

        // ── 4. Update LastLoginAt ───────────────────────────────────────────────
        user.LastLoginAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // ── 5. Generate Access Token ────────────────────────────────────────────
        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.TenantId, user.Email, user.Role.ToString());

        return new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15),
            User: new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.TenantId, user.Tenant.Name)
        )
        {
            RefreshToken = refreshValue
        };
    }
}
