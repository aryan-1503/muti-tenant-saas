using FluentValidation;
using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Auth.InviteUser;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Admin/Manager sends an invite email to a new team member.
/// Creates an InviteToken record and emails the invite URL.
/// The invited user follows the link, sets their password, and gets a full account.
/// </summary>
public record InviteUserCommand(
    string Email,
    string FullName,
    UserRole Role
) : IRequest<Guid>;   // Returns the InviteToken Id

// ─── Validator ────────────────────────────────────────────────────────────────
public class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role)
            .Must(r => r is UserRole.Manager or UserRole.Staff or UserRole.ReadOnly)
            .WithMessage("You can only invite users with Manager, Staff, or ReadOnly roles.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public InviteUserCommandHandler(
        IAppDbContext db,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant,
        IEmailService emailService,
        IConfiguration config)
    {
        _db = db;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _emailService = emailService;
        _config = config;
    }

    public async Task<Guid> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        // Only Admins can invite
        if (_currentUser.Role is not (UserRole.Admin or UserRole.Manager))
            throw new UnauthorizedAccessException("Only Admins and Managers can send invitations.");

        // Check email not already taken in this tenant
        var alreadyMember = await _db.Users
            .AnyAsync(u => u.Email == request.Email.ToLower(), cancellationToken);

        if (alreadyMember)
            throw new InvalidOperationException($"{request.Email} is already a member of your organisation.");

        // Expire any previous pending invites for this email in this tenant
        var previousInvites = await _db.InviteTokens
            .Where(it => it.Email == request.Email.ToLower() && it.AcceptedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var old in previousInvites)
            old.ExpiresAt = DateTime.UtcNow;   // Expire immediately

        // Create new invite token (48-hour expiry)
        var invite = new InviteToken
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.TenantId,
            Email = request.Email.ToLower().Trim(),
            FullName = request.FullName.Trim(),
            Role = request.Role,
            InvitedByUserId = _currentUser.UserId,
            Token = Guid.NewGuid().ToString("N"),  // Simple URL-safe token
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        };
        _db.InviteTokens.Add(invite);

        // Get org name for the email
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == _currentTenant.TenantId, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Send invitation email
        var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:4200";
        var inviteUrl = $"{frontendBase}/accept-invite?token={invite.Token}";

        await _emailService.SendInviteAsync(invite.Email, tenant.Name, inviteUrl, cancellationToken);

        return invite.Id;
    }
}
