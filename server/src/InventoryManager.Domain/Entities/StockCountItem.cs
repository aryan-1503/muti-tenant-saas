using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// One line in a stock count — one product to be counted.
/// SystemQuantity = what the system expected at count creation time.
/// CountedQuantity = what the staff member physically counted.
/// Variance = CountedQuantity - SystemQuantity (negative = loss, positive = found stock).
/// </summary>
public class StockCountItem : BaseEntity
{
    public Guid StockCountId { get; set; }
    public Guid ProductId { get; set; }
    public decimal SystemQuantity { get; set; }       // Snapshot at count creation
    public decimal? CountedQuantity { get; set; }     // Filled in by staff
    public decimal? Variance => CountedQuantity.HasValue
        ? CountedQuantity.Value - SystemQuantity
        : null;
    public bool? IsApproved { get; set; }              // Manager approval: true=approve, false=reject/recount
    public string? Notes { get; set; }

    // Navigation
    public StockCount StockCount { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
