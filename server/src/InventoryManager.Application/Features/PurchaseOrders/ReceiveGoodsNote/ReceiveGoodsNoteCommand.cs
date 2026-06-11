using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.PurchaseOrders.ReceiveGoodsNote;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Records the physical receipt of goods from a supplier (Goods Receipt Note / GRN).
/// This is the critical moment where goods physically enter the warehouse.
///
/// WHAT HAPPENS IN ONE TRANSACTION:
/// 1. Create the GRN header + lines.
/// 2. For each line:
///    a. Upsert StockLevel at the delivery warehouse (add QuantityReceived).
///    b. Accumulate QuantityReceived on the PO line.
///    c. Write a GoodsIn StockMovement.
/// 3. Update PO status to PartiallyReceived or FullyReceived.
/// 4. Low-stock check (shouldn't trigger on GRN, but coded defensively).
///
/// Partial deliveries are fully supported — one PO can have many GRNs.
/// Over-delivery is blocked (cannot receive more than ordered per line).
/// </summary>
public record ReceiveGoodsNoteCommand(
    Guid PurchaseOrderId,
    Guid WarehouseId,
    List<GrnLineInput> Lines,
    string? Notes
) : IRequest<Guid>;   // Returns the GRN Id

public record GrnLineInput(
    Guid PurchaseOrderLineId,
    Guid ProductId,
    decimal QuantityReceived,
    string? Condition,   // "Good", "Damaged", "Expired"
    string? Notes
);

// ─── Validator ────────────────────────────────────────────────────────────────
public class ReceiveGoodsNoteCommandValidator : AbstractValidator<ReceiveGoodsNoteCommand>
{
    public ReceiveGoodsNoteCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A GRN must have at least one line.")
            .Must(l => l.All(i => i.QuantityReceived > 0))
            .WithMessage("All received quantities must be greater than zero.");
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ReceiveGoodsNoteCommandHandler : IRequestHandler<ReceiveGoodsNoteCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReceiveGoodsNoteCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(ReceiveGoodsNoteCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot record goods receipts.");

        // ── Load PO with all lines ────────────────────────────────────────────
        var po = await _db.PurchaseOrders
            .Include(p => p.Lines).ThenInclude(l => l.Product)
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order not found.");

        if (po.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Closed
                      or PurchaseOrderStatus.FullyReceived)
            throw new InvalidOperationException(
                $"Cannot receive goods against a PO with status '{po.Status}'.");

        if (po.Status is PurchaseOrderStatus.Draft)
            throw new InvalidOperationException(
                "Cannot receive goods against a Draft PO — approve/send it to the supplier first.");

        // Validate receiving warehouse
        var warehouseActive = await _db.Warehouses
            .AnyAsync(w => w.Id == request.WarehouseId && w.IsActive, cancellationToken);
        if (!warehouseActive)
            throw new KeyNotFoundException("Receiving warehouse not found or is inactive.");

        // ── Build PO line lookup ──────────────────────────────────────────────
        var poLineDict = po.Lines.ToDictionary(l => l.Id);

        // ── Validate over-delivery ────────────────────────────────────────────
        var overDelivery = new List<string>();
        foreach (var grnLine in request.Lines)
        {
            if (!poLineDict.TryGetValue(grnLine.PurchaseOrderLineId, out var poLine))
                throw new KeyNotFoundException(
                    $"PO Line '{grnLine.PurchaseOrderLineId}' not found on this purchase order.");

            var outstanding = poLine.QuantityOrdered - poLine.QuantityReceived;
            if (grnLine.QuantityReceived > outstanding)
                overDelivery.Add(
                    $"'{poLine.Product.Name}': trying to receive {grnLine.QuantityReceived} " +
                    $"but only {outstanding} outstanding.");
        }

        if (overDelivery.Count > 0)
            throw new InvalidOperationException(
                "Over-delivery detected:\n" + string.Join("\n", overDelivery));

        // ── Load stock levels for the receiving warehouse ─────────────────────
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var stockLevels = await _db.StockLevels
            .Include(sl => sl.Product)
            .Where(sl => sl.WarehouseId == request.WarehouseId
                      && productIds.Contains(sl.ProductId))
            .ToDictionaryAsync(sl => sl.ProductId, cancellationToken);

        // ── Create GRN header ─────────────────────────────────────────────────
        var grn = new GoodsReceiptNote
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = request.PurchaseOrderId,
            WarehouseId = request.WarehouseId,
            ReceivedByUserId = _currentUser.UserId,
            ReceivedAt = DateTime.UtcNow,
            Notes = request.Notes?.Trim()
        };
        _db.GoodsReceiptNotes.Add(grn);

        // ── Process each GRN line ─────────────────────────────────────────────
        foreach (var grnLine in request.Lines)
        {
            var poLine = poLineDict[grnLine.PurchaseOrderLineId];

            // Create GRN line
            _db.GoodsReceiptNoteLines.Add(new GoodsReceiptNoteLine
            {
                Id = Guid.NewGuid(),
                GoodsReceiptNoteId = grn.Id,
                PurchaseOrderLineId = grnLine.PurchaseOrderLineId,
                ProductId = grnLine.ProductId,
                QuantityReceived = grnLine.QuantityReceived,
                Condition = grnLine.Condition,
                Notes = grnLine.Notes
            });

            // Accumulate received on PO line
            poLine.QuantityReceived += grnLine.QuantityReceived;

            // Upsert stock level
            if (stockLevels.TryGetValue(grnLine.ProductId, out var stockLevel))
            {
                stockLevel.QuantityOnHand += grnLine.QuantityReceived;
            }
            else
            {
                stockLevel = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = grnLine.ProductId,
                    WarehouseId = request.WarehouseId,
                    QuantityOnHand = grnLine.QuantityReceived,
                    QuantityInTransit = 0
                };
                _db.StockLevels.Add(stockLevel);
                stockLevels[grnLine.ProductId] = stockLevel;
            }

            // Write GoodsIn movement
            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = grnLine.ProductId,
                WarehouseId = request.WarehouseId,
                MovementType = MovementType.GoodsIn,
                QuantityChange = grnLine.QuantityReceived,
                QuantityAfter = stockLevel.QuantityOnHand,
                ReferenceType = "GRN",
                ReferenceId = grn.Id,
                Notes = grnLine.Condition != null && grnLine.Condition != "Good"
                    ? $"GRN from {po.Supplier.Name}. Condition: {grnLine.Condition}."
                    : $"GRN from {po.Supplier.Name}.",
                CreatedByUserId = _currentUser.UserId
            });
        }

        // ── Update PO status ──────────────────────────────────────────────────
        var isFullyReceived = po.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
        po.Status = isFullyReceived
            ? PurchaseOrderStatus.FullyReceived
            : PurchaseOrderStatus.PartiallyReceived;

        await _db.SaveChangesAsync(cancellationToken);
        return grn.Id;
    }
}
