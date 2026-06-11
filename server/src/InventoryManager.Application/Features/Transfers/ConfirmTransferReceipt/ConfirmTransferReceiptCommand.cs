using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Transfers.ConfirmTransferReceipt;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Confirms that goods have arrived at the destination warehouse.
///
/// WHAT HAPPENS IN ONE TRANSACTION:
/// 1. Validate transfer exists and is InTransit.
/// 2. For each line, the caller provides QuantityReceived (may differ from requested
///    if goods were damaged or partially lost in transit).
/// 3. For each line:
///    a. Clear QuantityInTransit from the destination StockLevel.
///    b. Add QuantityReceived to QuantityOnHand at destination.
///    c. Write a TransferIn StockMovement at the destination.
///    d. If quantity is short, write the shortfall in the line notes.
/// 4. Set Transfer status = Completed.
/// 5. Check low-stock for any new stock that arrives below reorder point.
/// </summary>
public record ConfirmTransferReceiptCommand(
    Guid TransferId,
    List<TransferReceiptLine> ReceivedLines,
    string? Notes
) : IRequest;

public record TransferReceiptLine(Guid ProductId, decimal QuantityReceived, string? Notes);

// ─── Validator ────────────────────────────────────────────────────────────────
public class ConfirmTransferReceiptCommandValidator : AbstractValidator<ConfirmTransferReceiptCommand>
{
    public ConfirmTransferReceiptCommandValidator()
    {
        RuleFor(x => x.TransferId).NotEmpty();
        RuleFor(x => x.ReceivedLines)
            .NotEmpty().WithMessage("At least one received line is required.")
            .Must(l => l.All(r => r.QuantityReceived >= 0))
            .WithMessage("Received quantities cannot be negative.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ConfirmTransferReceiptCommandHandler : IRequestHandler<ConfirmTransferReceiptCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ConfirmTransferReceiptCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(ConfirmTransferReceiptCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot confirm transfer receipts.");

        // ── Load transfer with all lines ──────────────────────────────────────
        var transfer = await _db.StockTransfers
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .FirstOrDefaultAsync(t => t.Id == request.TransferId, cancellationToken)
            ?? throw new KeyNotFoundException("Transfer not found.");

        if (transfer.Status != TransferStatus.InTransit)
            throw new InvalidOperationException(
                $"Cannot confirm receipt — transfer is currently '{transfer.Status}'. " +
                "Only InTransit transfers can be confirmed.");

        // ── Validate all lines in receipt match the transfer ──────────────────
        var receivedDict = request.ReceivedLines.ToDictionary(r => r.ProductId);
        foreach (var line in transfer.Lines)
        {
            if (!receivedDict.ContainsKey(line.ProductId))
                throw new InvalidOperationException(
                    $"Missing receipt entry for product '{line.Product.Name}'. " +
                    "All transfer lines must be accounted for (enter 0 if nothing received).");
        }

        // ── Load destination stock levels ─────────────────────────────────────
        var productIds = transfer.Lines.Select(l => l.ProductId).ToList();
        var destLevels = await _db.StockLevels
            .Include(sl => sl.Product)
            .Where(sl => sl.WarehouseId == transfer.ToWarehouseId
                      && productIds.Contains(sl.ProductId))
            .ToDictionaryAsync(sl => sl.ProductId, cancellationToken);

        // ── Process each line ─────────────────────────────────────────────────
        var lowStockProducts = new List<(string Name, string Sku, decimal Qty, decimal Reorder)>();

        foreach (var transferLine in transfer.Lines)
        {
            var received = receivedDict[transferLine.ProductId];
            var qtyReceived = received.QuantityReceived;

            // Update the line with actual received quantity
            transferLine.QuantityReceived = qtyReceived;
            transferLine.Notes = received.Notes;

            if (!destLevels.TryGetValue(transferLine.ProductId, out var destLevel))
            {
                // Shouldn't happen (CreateTransfer creates it), but handle defensively
                destLevel = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = transferLine.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    QuantityOnHand = 0,
                    QuantityInTransit = 0
                };
                _db.StockLevels.Add(destLevel);
                destLevels[transferLine.ProductId] = destLevel;
            }

            // Clear in-transit quantity (the full requested amount leaves "in transit" state)
            // regardless of what actually arrived — shortfall is a discrepancy to be noted
            destLevel.QuantityInTransit = Math.Max(0,
                destLevel.QuantityInTransit - transferLine.QuantityRequested);

            // Credit the actual received quantity
            var previousQty = destLevel.QuantityOnHand;
            destLevel.QuantityOnHand += qtyReceived;

            // Write TransferIn audit movement
            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = transferLine.ProductId,
                WarehouseId = transfer.ToWarehouseId,
                MovementType = MovementType.TransferIn,
                QuantityChange = qtyReceived,
                QuantityAfter = destLevel.QuantityOnHand,
                ReferenceType = "Transfer",
                ReferenceId = transfer.Id,
                Notes = qtyReceived < transferLine.QuantityRequested
                    ? $"Received {qtyReceived} of {transferLine.QuantityRequested} requested from " +
                      $"'{transfer.FromWarehouse.Name}'. Shortfall: {transferLine.QuantityRequested - qtyReceived}."
                    : $"Received from '{transfer.FromWarehouse.Name}'.",
                CreatedByUserId = _currentUser.UserId
            });

            // Low-stock check for destination after receipt
            var product = transferLine.Product;
            var wasAbove = previousQty > product.ReorderPoint;
            if (wasAbove && destLevel.QuantityOnHand <= product.ReorderPoint)
            {
                lowStockProducts.Add((product.Name, product.SkuCode,
                    destLevel.QuantityOnHand, product.ReorderPoint));
            }
        }

        // ── Complete the transfer ─────────────────────────────────────────────
        transfer.Status = TransferStatus.Completed;
        transfer.CompletedAt = DateTime.UtcNow;

        // ── Low-stock notifications if triggered ──────────────────────────────
        if (lowStockProducts.Count > 0)
        {
            var managers = await _db.Users
                .Where(u => u.IsActive &&
                            (u.Role == UserRole.Admin || u.Role == UserRole.Manager))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var userId in managers)
            {
                foreach (var (name, sku, qty, reorder) in lowStockProducts)
                {
                    _db.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Type = NotificationType.LowStock,
                        Message = $"After transfer receipt at '{transfer.ToWarehouse.Name}': " +
                                  $"'{name}' (SKU: {sku}) has {qty} units — at or below reorder point of {reorder}.",
                        ReferenceType = "Transfer",
                        ReferenceId = transfer.Id,
                        IsRead = false
                    });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
