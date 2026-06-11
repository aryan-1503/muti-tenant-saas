using FluentValidation;
using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Application.Common.Models;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.AcceptInvite;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Called when the invited user clicks the link in their email and sets their password.
/// Validates the invite token, creates the user account, and logs them in immediately.
/// </summary>
public record AcceptInviteCommand(
    string Token,
    string Password,
    string ConfirmPassword
) : IRequest<AuthResponse>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class AcceptInviteCommandValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class AcceptInviteCommandHandler : IRequestHandler<AcceptInviteCommand, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public AcceptInviteCommandHandler(IAppDbContext db, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _config = config;
    }

    public async Task<AuthResponse> Handle(AcceptInviteCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Validate the invite token ──────────────────────────────────────
        var invite = await _db.InviteTokens
            .IgnoreQueryFilters()
            .Include(it => it.Tenant)
            .FirstOrDefaultAsync(it => it.Token == request.Token, cancellationToken);

        if (invite is null)
            throw new KeyNotFoundException("Invitation not found. Please request a new invite.");

        if (invite.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("This invitation has expired. Please request a new invite.");

        if (invite.AcceptedAt is not null)
            throw new InvalidOperationException("This invitation has already been used.");

        // ── 2. Ensure email not already taken ─────────────────────────────────
        var emailTaken = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == invite.Email && u.TenantId == invite.TenantId, cancellationToken);

        if (emailTaken)
            throw new InvalidOperationException("An account with this email already exists in your organisation.");

        // ── 3. Create the user ────────────────────────────────────────────────
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = invite.TenantId,
            Email = invite.Email,
            FullName = invite.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = invite.Role,
            IsActive = true
        };
        _db.Users.Add(user);

        // ── 4. Mark invite as accepted ────────────────────────────────────────
        invite.AcceptedAt = DateTime.UtcNow;

        // ── 5. Issue refresh token ────────────────────────────────────────────
        var refreshValue = _tokenService.GenerateRefreshToken();
        var expiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        _db.RefreshTokens.Add(new InventoryManager.Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = invite.TenantId,
            UserId = user.Id,
            Token = refreshValue,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        });

        await _db.SaveChangesAsync(cancellationToken);

        // ── 6. Generate access token and return ───────────────────────────────
        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.TenantId, user.Email, user.Role.ToString());

        return new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15),
            User: new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.TenantId, invite.Tenant.Name)
        )
        {
            RefreshToken = refreshValue
        };
    }
}
