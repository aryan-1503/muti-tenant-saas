using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Products.DeleteProduct;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Soft-deletes a product (IsActive = false).
/// Hard delete is blocked because the product is referenced by:
/// - StockMovements (audit trail)
/// - PurchaseOrderLines, GoodsReceiptNoteLines (procurement history)
/// - StockCountItems (count history)
/// Deactivated products are hidden from most views but preserved for reporting.
/// </summary>
public record DeleteProductCommand(Guid Id) : IRequest;

// ─── Handler ──────────────────────────────────────────────────────────────────
public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteProductCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly or Domain.Enums.UserRole.Staff)
            throw new UnauthorizedAccessException("Only Managers and Admins can delete products.");

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        if (!product.IsActive)
            throw new InvalidOperationException("Product is already inactive.");

        // Warn if product has stock on hand — still allow but document it
        var hasStock = await _db.StockLevels
            .AnyAsync(sl => sl.ProductId == request.Id && sl.QuantityOnHand > 0, cancellationToken);

        if (hasStock)
            throw new InvalidOperationException(
                "Cannot deactivate a product that has stock on hand. " +
                "Please write off or transfer all stock before deactivating.");

        product.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
