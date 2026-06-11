using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Application.Common.Models;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.RefreshToken;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Rotates the refresh token.
/// The old token is revoked and a brand-new one is issued.
/// This is the industry-standard "refresh token rotation" pattern — if a stolen
/// token is used, the legitimate user's next refresh will fail (the token is gone),
/// alerting them to log in again.
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

// ─── Handler ──────────────────────────────────────────────────────────────────
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public RefreshTokenCommandHandler(IAppDbContext db, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _config = config;
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Find the token in the database ─────────────────────────────────
        var stored = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(rt => rt.User)
                .ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (stored is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // ── 2. Check if already revoked (possible token theft replay) ──────────
        if (stored.RevokedAt is not null)
        {
            // SECURITY: If a revoked token is reused, revoke ALL tokens for that user.
            // This forces them to log in again — any attacker loses access too.
            var allUserTokens = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == stored.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var t in allUserTokens)
                t.RevokedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token has already been used. Please log in again.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired. Please log in again.");

        if (!stored.User.IsActive || !stored.User.Tenant.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        // ── 3. Revoke the current token ────────────────────────────────────────
        stored.RevokedAt = DateTime.UtcNow;

        // ── 4. Issue a new refresh token (rotation) ────────────────────────────
        var newRefreshValue = _tokenService.GenerateRefreshToken();
        var expiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        _db.RefreshTokens.Add(new InventoryManager.Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = stored.TenantId,
            UserId = stored.UserId,
            Token = newRefreshValue,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        });

        await _db.SaveChangesAsync(cancellationToken);

        // ── 5. Issue new access token ──────────────────────────────────────────
        var user = stored.User;
        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.TenantId, user.Email, user.Role.ToString());

        return new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15),
            User: new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.TenantId, user.Tenant.Name)
        )
        {
            RefreshToken = newRefreshValue
        };
    }
}
