using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.PurchaseOrders.ApprovePurchaseOrder;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Moves a PO from Draft → Sent status, indicating it has been sent to the supplier.
/// Only Managers and Admins can approve/send POs.
/// A Confirmed PO has been verbally/formally acknowledged by the supplier.
/// </summary>
public record ApprovePurchaseOrderCommand(
    Guid Id,
    bool MarkAsConfirmed = false   // true = go straight to Confirmed (supplier already confirmed verbally)
) : IRequest;

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ApprovePurchaseOrderCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.Staff or UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Only Managers and Admins can approve purchase orders.");

        var po = await _db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order not found.");

        if (po.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException(
                $"Only Draft purchase orders can be approved/sent. " +
                $"Current status: {po.Status}.");

        po.Status = request.MarkAsConfirmed
            ? PurchaseOrderStatus.Confirmed
            : PurchaseOrderStatus.Sent;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
