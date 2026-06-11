using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Suppliers.UpdateSupplier;

public record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    int? LeadTimeDays,
    string? PaymentTerms,
    string? Notes,
    bool IsActive
) : IRequest;

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Supplier email must be a valid email address.");
        RuleFor(x => x.LeadTimeDays).GreaterThan(0).When(x => x.LeadTimeDays.HasValue);
    }
}

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot update suppliers.");

        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Supplier not found.");

        var nameConflict = await _db.Suppliers
            .AnyAsync(s => s.Name.ToLower() == request.Name.ToLower().Trim()
                        && s.Id != request.Id, cancellationToken);
        if (nameConflict)
            throw new InvalidOperationException($"A supplier named '{request.Name}' already exists.");

        supplier.Name = request.Name.Trim();
        supplier.ContactName = request.ContactName?.Trim();
        supplier.Email = request.Email?.Trim().ToLower();
        supplier.Phone = request.Phone?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.LeadTimeDays = request.LeadTimeDays;
        supplier.PaymentTerms = request.PaymentTerms?.Trim();
        supplier.Notes = request.Notes?.Trim();
        supplier.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
