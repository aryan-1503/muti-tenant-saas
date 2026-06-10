using InventoryManager.Domain.Common;
using InventoryManager.Domain.Enums;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A formal order to a supplier. Status flows from Draft → Sent → Confirmed →
/// PartiallyReceived / FullyReceived → Closed.
/// GRNs are created against this PO when goods arrive.
/// </summary>
public class PurchaseOrder : TenantScopedEntity
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }   // Delivery destination
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
    public ICollection<PurchaseOrderLine> Lines { get; set; } = [];
    public ICollection<GoodsReceiptNote> GoodsReceiptNotes { get; set; } = [];
}
