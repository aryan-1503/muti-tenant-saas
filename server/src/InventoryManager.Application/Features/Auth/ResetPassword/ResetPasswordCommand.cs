using FluentValidation;
using InventoryManager.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.ResetPassword;

// ─── Command ──────────────────────────────────────────────────────────────────
public record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string ConfirmPassword
) : IRequest;

// ─── Validator ────────────────────────────────────────────────────────────────
public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IAppDbContext _db;

    public ResetPasswordCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException("Invalid or expired password reset link.");

        if (user.PasswordResetTokenExpiresAt is null || user.PasswordResetTokenExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("This password reset link has expired. Please request a new one.");

        // ── Update password ───────────────────────────────────────────────────
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // ── Clear reset token so it can't be reused ───────────────────────────
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;

        // ── Revoke all active refresh tokens (forces re-login everywhere) ─────
        var activeTokens = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
