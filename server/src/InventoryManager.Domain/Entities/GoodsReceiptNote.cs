using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// Records the physical receipt of goods against a PurchaseOrder.
/// A single PO can have multiple GRNs (partial deliveries are common).
/// Creating a GRN triggers stock level updates — this is the moment goods enter the system.
/// </summary>
public class GoodsReceiptNote : TenantScopedEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public AppUser ReceivedByUser { get; set; } = null!;
    public ICollection<GoodsReceiptNoteLine> Lines { get; set; } = [];
}
