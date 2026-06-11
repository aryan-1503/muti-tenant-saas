using FluentValidation;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Warehouses.CreateWarehouse;

// ─── Command ──────────────────────────────────────────────────────────────────
public record CreateWarehouseCommand(
    string Name,
    string? Address,
    string? Type
) : IRequest<Guid>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
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
public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateWarehouseCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        // Only Admin or Manager can create warehouses
        if (_currentUser.Role is Domain.Enums.UserRole.Staff or Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("You do not have permission to create warehouses.");

        // Prevent duplicate names within the same tenant
        var nameExists = await _db.Warehouses
            .AnyAsync(w => w.Name.ToLower() == request.Name.ToLower().Trim(), cancellationToken);

        if (nameExists)
            throw new InvalidOperationException($"A warehouse named '{request.Name}' already exists.");

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Type = request.Type?.Trim(),
            IsActive = true
            // TenantId is auto-stamped by AppDbContext.SaveChangesAsync
        };

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(cancellationToken);

        return warehouse.Id;
    }
}
