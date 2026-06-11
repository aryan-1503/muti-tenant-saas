using InventoryManager.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.RevokeToken;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>Logout — revokes the current refresh token so it can never be used again.</summary>
public record RevokeTokenCommand(string RefreshToken) : IRequest;

// ─── Handler ──────────────────────────────────────────────────────────────────
public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand>
{
    private readonly IAppDbContext _db;

    public RevokeTokenCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        // Silently succeed even if token not found — idempotent logout
    }
}
