using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// The current stock level for a specific product at a specific warehouse.
/// One row per product+warehouse combination.
/// QuantityOnHand = physically present and available.
/// QuantityInTransit = dispatched in a transfer but not yet received at this warehouse.
/// </summary>
public class StockLevel : TenantScopedEntity
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityInTransit { get; set; }  // Stock en route to this warehouse

    // Navigation
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
