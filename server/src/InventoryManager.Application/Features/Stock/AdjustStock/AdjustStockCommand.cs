using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Stock.AdjustStock;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Performs a manual stock adjustment at a warehouse.
/// Used for: corrections, write-offs, damaged goods, found stock, etc.
/// 
/// RULES:
/// - QuantityChange: positive = add stock, negative = remove stock.
/// - Reason is REQUIRED — auditors must know why stock changed.
/// - Cannot reduce stock below zero.
/// - After adjustment, if stock falls at or below ReorderPoint, a low-stock
///   notification is created for all Admins and Managers of this tenant.
/// - MovementType can be ManualAdjustment or WriteOff (caller decides context).
/// </summary>
public record AdjustStockCommand(
    Guid ProductId,
    Guid WarehouseId,
    decimal QuantityChange,
    MovementType MovementType,
    string Reason,
    string? Notes
) : IRequest<Guid>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.QuantityChange)
            .NotEqual(0).WithMessage("Adjustment quantity cannot be zero.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for all manual adjustments.")
            .MaximumLength(500);
        RuleFor(x => x.MovementType)
            .Must(t => t is MovementType.ManualAdjustment or MovementType.WriteOff)
            .WithMessage("Adjustment type must be ManualAdjustment or WriteOff.");
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdjustStockCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot adjust stock.");

        // ── Load StockLevel with related Product for reorder check ────────────
        var stockLevel = await _db.StockLevels
            .Include(sl => sl.Product)
            .FirstOrDefaultAsync(sl =>
                sl.ProductId == request.ProductId &&
                sl.WarehouseId == request.WarehouseId, cancellationToken)
            ?? throw new KeyNotFoundException(
                "No stock record exists for this product/warehouse combination. " +
                "Use Set Opening Balance first.");

        // ── Guard against negative stock ──────────────────────────────────────
        var newQty = stockLevel.QuantityOnHand + request.QuantityChange;
        if (newQty < 0)
            throw new InvalidOperationException(
                $"Adjustment would result in negative stock. " +
                $"Current: {stockLevel.QuantityOnHand}, Change: {request.QuantityChange}.");

        // ── Apply the adjustment ──────────────────────────────────────────────
        stockLevel.QuantityOnHand = newQty;

        // ── Write immutable audit movement ────────────────────────────────────
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            MovementType = request.MovementType,
            QuantityChange = request.QuantityChange,
            QuantityAfter = newQty,
            Reason = request.Reason,
            Notes = request.Notes,
            CreatedByUserId = _currentUser.UserId
        };
        _db.StockMovements.Add(movement);

        // ── Low-stock notification logic ──────────────────────────────────────
        var product = stockLevel.Product;
        var wasAboveReorder = (stockLevel.QuantityOnHand - request.QuantityChange) > product.ReorderPoint;
        var isNowAtOrBelowReorder = newQty <= product.ReorderPoint;

        if (wasAboveReorder && isNowAtOrBelowReorder)
        {
            // Stock just crossed the reorder threshold — notify all Admins and Managers
            var managersToNotify = await _db.Users
                .Where(u => u.IsActive &&
                            (u.Role == UserRole.Admin || u.Role == UserRole.Manager))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var userId in managersToNotify)
            {
                _db.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Type = NotificationType.LowStock,
                    Message = $"Low stock alert: '{product.Name}' (SKU: {product.SkuCode}) has " +
                              $"{newQty} {product.UnitOfMeasure} remaining — at or below reorder point " +
                              $"of {product.ReorderPoint}.",
                    ReferenceType = "Product",
                    ReferenceId = product.Id,
                    IsRead = false
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return movement.Id;
    }
}
