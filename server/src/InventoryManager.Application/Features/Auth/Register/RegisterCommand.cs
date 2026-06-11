using FluentValidation;
using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Application.Common.Models;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.Register;

// ─── Command ─────────────────────────────────────────────────────────────────
/// <summary>
/// Creates a brand-new tenant + the first admin user in a single transaction.
/// This is the "Sign Up" flow — called once per organisation.
/// </summary>
public record RegisterCommand(
    string OrganisationName,
    string FullName,
    string Email,
    string Password,
    string Industry,
    string CurrencyCode,
    string TimezoneId
) : IRequest<AuthResponse>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.OrganisationName)
            .NotEmpty().WithMessage("Organisation name is required.")
            .MaximumLength(200);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Your name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one number.");

        RuleFor(x => x.Industry).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).WithMessage("Currency code must be 3 characters (e.g. USD).");
        RuleFor(x => x.TimezoneId).NotEmpty();
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public RegisterCommandHandler(IAppDbContext db, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _config = config;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Uniqueness check (email must be globally unique at registration) ──
        var emailTaken = await _db.Users
            .IgnoreQueryFilters()  // No tenant filter yet — this is registration
            .AnyAsync(u => u.Email == request.Email.ToLower(), cancellationToken);

        if (emailTaken)
            throw new InvalidOperationException("An account with this email address already exists.");

        // ── 2. Create Tenant ──────────────────────────────────────────────────
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.OrganisationName.Trim(),
            Industry = request.Industry,
            CurrencyCode = request.CurrencyCode.ToUpper(),
            TimezoneId = request.TimezoneId,
            IsActive = true
        };
        _db.Tenants.Add(tenant);

        // ── 3. Create Admin User ──────────────────────────────────────────────
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = request.Email.ToLower().Trim(),
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Admin,
            IsActive = true
        };
        _db.Users.Add(user);

        // ── 4. Issue Refresh Token ────────────────────────────────────────────
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var expiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        var refreshToken = new InventoryManager.Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        };
        _db.RefreshTokens.Add(refreshToken);

        // ── 5. Persist everything in one transaction ──────────────────────────
        await _db.SaveChangesAsync(cancellationToken);

        // ── 6. Generate Access Token ──────────────────────────────────────────
        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, tenant.Id, user.Email, user.Role.ToString());

        return new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(15),
            User: new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), tenant.Id, tenant.Name)
        )
        {
            RefreshToken = refreshTokenValue  // Controller will set this as HttpOnly cookie
        };
    }
}
