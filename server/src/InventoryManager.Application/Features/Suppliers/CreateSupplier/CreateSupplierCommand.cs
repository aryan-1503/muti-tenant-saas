using FluentValidation;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Suppliers.CreateSupplier;

public record CreateSupplierCommand(
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    int? LeadTimeDays,
    string? PaymentTerms,
    string? Notes
) : IRequest<Guid>;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Supplier email must be a valid email address.");
        RuleFor(x => x.ContactName).MaximumLength(200).When(x => x.ContactName is not null);
        RuleFor(x => x.Phone).MaximumLength(50).When(x => x.Phone is not null);
        RuleFor(x => x.Address).MaximumLength(500).When(x => x.Address is not null);
        RuleFor(x => x.LeadTimeDays).GreaterThan(0).When(x => x.LeadTimeDays.HasValue)
            .WithMessage("Lead time must be a positive number of days.");
        RuleFor(x => x.PaymentTerms).MaximumLength(100).When(x => x.PaymentTerms is not null);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot create suppliers.");

        var nameExists = await _db.Suppliers
            .AnyAsync(s => s.Name.ToLower() == request.Name.ToLower().Trim(), cancellationToken);
        if (nameExists)
            throw new InvalidOperationException($"A supplier named '{request.Name}' already exists.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            ContactName = request.ContactName?.Trim(),
            Email = request.Email?.Trim().ToLower(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            LeadTimeDays = request.LeadTimeDays,
            PaymentTerms = request.PaymentTerms?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true
        };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);
        return supplier.Id;
    }
}
