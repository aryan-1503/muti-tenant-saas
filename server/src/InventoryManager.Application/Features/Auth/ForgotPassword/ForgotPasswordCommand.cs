using FluentValidation;
using InventoryManager.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.ForgotPassword;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Initiates a password reset by emailing a reset link.
/// ALWAYS returns success — even if email not found (prevents user enumeration attacks).
/// The reset token is stored in the DB (PasswordResetToken field on AppUser).
/// </summary>
public record ForgotPasswordCommand(string Email) : IRequest;

// ─── Validator ────────────────────────────────────────────────────────────────
public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IAppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public ForgotPasswordCommandHandler(IAppDbContext db, IEmailService emailService, IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // ── Find the user (ignore tenant filter — user provides only email) ───
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), cancellationToken);

        // SECURITY: If user not found, do NOT throw. Return silently.
        // This prevents attackers from discovering which emails are registered.
        if (user is null || !user.IsActive)
            return;

        // ── Generate a cryptographically secure reset token ───────────────────
        var resetToken = Guid.NewGuid().ToString("N");  // 32 hex chars
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);  // 1-hour expiry

        await _db.SaveChangesAsync(cancellationToken);

        // ── Send reset email ──────────────────────────────────────────────────
        var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:4200";
        var resetUrl = $"{frontendBase}/reset-password?token={resetToken}";

        await _emailService.SendPasswordResetAsync(
            user.Email, user.FullName, resetUrl, cancellationToken);
    }
}
