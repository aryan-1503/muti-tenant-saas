using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Suppliers.DeleteSupplier;

/// <summary>
/// Soft-deletes (deactivates) a supplier.
/// Hard delete is blocked because historical POs reference this supplier.
/// A supplier with open (non-closed, non-cancelled) POs cannot be deactivated.
/// </summary>
public record DeleteSupplierCommand(Guid Id) : IRequest;

public class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteSupplierCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not Domain.Enums.UserRole.Admin)
            throw new UnauthorizedAccessException("Only Admins can deactivate suppliers.");

        var supplier = await _db.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Supplier not found.");

        if (!supplier.IsActive)
            throw new InvalidOperationException("Supplier is already inactive.");

        // Block deactivation if there are open POs
        var openPoCount = await _db.PurchaseOrders
            .CountAsync(po => po.SupplierId == request.Id &&
                po.Status != Domain.Enums.PurchaseOrderStatus.Closed &&
                po.Status != Domain.Enums.PurchaseOrderStatus.Cancelled &&
                po.Status != Domain.Enums.PurchaseOrderStatus.FullyReceived,
                cancellationToken);

        if (openPoCount > 0)
            throw new InvalidOperationException(
                $"Cannot deactivate supplier '{supplier.Name}' — they have {openPoCount} open purchase order(s). " +
                "Close or cancel those orders first.");

        supplier.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
