using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Organisation.UpdateProfile;

// ─── Command ──────────────────────────────────────────────────────────────────
public record UpdateOrganisationProfileCommand(
    string Name,
    string? Industry,
    string CurrencyCode,
    string TimezoneId,
    string? LogoUrl
) : IRequest;

// ─── Validator ────────────────────────────────────────────────────────────────
public class UpdateOrganisationProfileCommandValidator
    : AbstractValidator<UpdateOrganisationProfileCommand>
{
    public UpdateOrganisationProfileCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organisation name is required.")
            .MaximumLength(200);

        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .Length(3).WithMessage("Currency code must be 3 characters (e.g. USD, INR, EUR).")
            .Matches("^[A-Z]{3}$").WithMessage("Currency code must be 3 uppercase letters.");

        RuleFor(x => x.TimezoneId)
            .NotEmpty().WithMessage("Timezone is required.");

        RuleFor(x => x.Industry)
            .MaximumLength(100).When(x => x.Industry is not null);

        RuleFor(x => x.LogoUrl)
            .MaximumLength(500)
            .Must(url => url is null || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Logo URL must be a valid absolute URL.")
            .When(x => x.LogoUrl is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class UpdateOrganisationProfileCommandHandler
    : IRequestHandler<UpdateOrganisationProfileCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly ICurrentUserService _currentUser;

    public UpdateOrganisationProfileCommandHandler(
        IAppDbContext db,
        ICurrentTenantService tenant,
        ICurrentUserService currentUser)
    {
        _db = db;
        _tenant = tenant;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateOrganisationProfileCommand request, CancellationToken cancellationToken)
    {
        // Only Admins can change org settings
        if (_currentUser.Role is not Domain.Enums.UserRole.Admin)
            throw new UnauthorizedAccessException("Only Admins can update organisation settings.");

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Organisation not found.");

        tenant.Name = request.Name.Trim();
        tenant.Industry = request.Industry?.Trim();
        tenant.CurrencyCode = request.CurrencyCode.ToUpper();
        tenant.TimezoneId = request.TimezoneId;
        tenant.LogoUrl = request.LogoUrl?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }
}
