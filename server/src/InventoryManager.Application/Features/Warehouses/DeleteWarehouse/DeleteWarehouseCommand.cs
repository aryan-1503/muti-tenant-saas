using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Warehouses.DeleteWarehouse;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Soft-deletes a warehouse by setting IsActive = false.
/// Hard delete is intentionally blocked because:
/// 1. Stock movements, GRNs, and transfers reference this warehouse historically.
/// 2. The audit trail must remain intact.
/// A warehouse with active stock cannot be deactivated — the user must first
/// transfer or write off all stock.
/// </summary>
public record DeleteWarehouseCommand(Guid Id) : IRequest;

// ─── Handler ──────────────────────────────────────────────────────────────────
public class DeleteWarehouseCommandHandler : IRequestHandler<DeleteWarehouseCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteWarehouseCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        // Only Admins can deactivate warehouses
        if (_currentUser.Role is not Domain.Enums.UserRole.Admin)
            throw new UnauthorizedAccessException("Only Admins can deactivate warehouses.");

        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Warehouse not found.");

        if (!warehouse.IsActive)
            throw new InvalidOperationException("Warehouse is already inactive.");

        // Block deactivation if warehouse has any stock on hand
        var hasStock = await _db.StockLevels
            .AnyAsync(sl => sl.WarehouseId == request.Id && sl.QuantityOnHand > 0, cancellationToken);

        if (hasStock)
            throw new InvalidOperationException(
                "Cannot deactivate a warehouse that has stock on hand. " +
                "Please transfer or write off all stock before deactivating.");

        // Soft delete — preserve all historical data
        warehouse.IsActive = false;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
