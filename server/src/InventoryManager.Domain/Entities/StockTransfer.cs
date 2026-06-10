using InventoryManager.Domain.Common;
using InventoryManager.Domain.Enums;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A request to move stock from one warehouse to another.
/// The two-step workflow (dispatch + confirm) is critical:
/// - When dispatched: source stock is deducted immediately (can't sell what you've shipped)
/// - When confirmed: destination stock is credited (can now use/sell what arrived)
/// Stock is "in transit" between these two events.
/// </summary>
public class StockTransfer : TenantScopedEntity
{
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
    public ICollection<StockTransferLine> Lines { get; set; } = [];
}
