using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Stock.SetOpeningBalance;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Sets the opening stock balance for a product at a specific warehouse.
/// This is the "Day 0" operation when a business first sets up their inventory.
/// 
/// DESIGN:
/// - UPSERTS the StockLevel row (create if not exists, update if already exists).
/// - Always creates a StockMovement of type OpeningBalance for full audit trail.
/// - If a balance already exists, a corrective movement captures the delta.
/// - Admins and Managers only (staff should not be seeding opening balances).
/// </summary>
public record SetOpeningBalanceCommand(
    Guid ProductId,
    Guid WarehouseId,
    decimal Quantity,
    string? Notes
) : IRequest<Guid>;  // Returns the StockMovement Id

// ─── Validator ────────────────────────────────────────────────────────────────
public class SetOpeningBalanceCommandValidator : AbstractValidator<SetOpeningBalanceCommand>
{
    public SetOpeningBalanceCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Opening balance cannot be negative.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class SetOpeningBalanceCommandHandler : IRequestHandler<SetOpeningBalanceCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SetOpeningBalanceCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(SetOpeningBalanceCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.Staff or UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Only Managers and Admins can set opening balances.");

        // ── Validate product and warehouse belong to this tenant ──────────────
        var productExists = await _db.Products
            .AnyAsync(p => p.Id == request.ProductId && p.IsActive, cancellationToken);
        if (!productExists)
            throw new KeyNotFoundException("Product not found or is inactive.");

        var warehouseExists = await _db.Warehouses
            .AnyAsync(w => w.Id == request.WarehouseId && w.IsActive, cancellationToken);
        if (!warehouseExists)
            throw new KeyNotFoundException("Warehouse not found or is inactive.");

        // ── Upsert StockLevel ─────────────────────────────────────────────────
        var stockLevel = await _db.StockLevels
            .FirstOrDefaultAsync(sl =>
                sl.ProductId == request.ProductId &&
                sl.WarehouseId == request.WarehouseId, cancellationToken);

        decimal previousQty = 0;
        decimal quantityChange;

        if (stockLevel is null)
        {
            // First time — create the row
            stockLevel = new StockLevel
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                QuantityOnHand = request.Quantity,
                QuantityInTransit = 0
            };
            _db.StockLevels.Add(stockLevel);
            quantityChange = request.Quantity;
        }
        else
        {
            // Already exists — this is a correction
            previousQty = stockLevel.QuantityOnHand;
            quantityChange = request.Quantity - previousQty;
            stockLevel.QuantityOnHand = request.Quantity;
        }

        // ── Write immutable audit movement ────────────────────────────────────
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            MovementType = MovementType.OpeningBalance,
            QuantityChange = quantityChange,
            QuantityAfter = request.Quantity,
            Notes = previousQty > 0
                ? $"Opening balance correction (was {previousQty}). {request.Notes}"
                : request.Notes,
            CreatedByUserId = _currentUser.UserId
        };
        _db.StockMovements.Add(movement);

        await _db.SaveChangesAsync(cancellationToken);
        return movement.Id;
    }
}
