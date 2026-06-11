using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Warehouses.UpdateWarehouse;

// ─── Command ──────────────────────────────────────────────────────────────────
public record UpdateWarehouseCommand(
    Guid Id,
    string Name,
    string? Address,
    string? Type
) : IRequest;

// ─── Validator ────────────────────────────────────────────────────────────────
public class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Warehouse name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .MaximumLength(500).When(x => x.Address is not null);

        RuleFor(x => x.Type)
            .MaximumLength(100).When(x => x.Type is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateWarehouseCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.Staff or Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("You do not have permission to update warehouses.");

        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Warehouse '{request.Id}' not found.");

        // Check new name doesn't clash with another warehouse
        var nameConflict = await _db.Warehouses
            .AnyAsync(w => w.Name.ToLower() == request.Name.ToLower().Trim()
                        && w.Id != request.Id, cancellationToken);

        if (nameConflict)
            throw new InvalidOperationException($"A warehouse named '{request.Name}' already exists.");

        warehouse.Name = request.Name.Trim();
        warehouse.Address = request.Address?.Trim();
        warehouse.Type = request.Type?.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }
}
