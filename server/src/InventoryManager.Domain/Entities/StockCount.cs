using InventoryManager.Domain.Common;
using InventoryManager.Domain.Enums;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A physical stock count session. Assigned to a user, scoped to a warehouse
/// (and optionally a category for cycle counts).
/// SystemQuantity is captured at creation time as the expected count.
/// IsBlindCount hides SystemQuantity from the counting staff to prevent anchoring bias.
/// </summary>
public class StockCount : TenantScopedEntity
{
    public Guid WarehouseId { get; set; }
    public Guid? CategoryId { get; set; }          // Null = count all products at the warehouse
    public Guid? AssignedUserId { get; set; }
    public bool IsBlindCount { get; set; } = true;  // Hide expected qty from counter by default
    public StockCountStatus Status { get; set; } = StockCountStatus.Created;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Warehouse Warehouse { get; set; } = null!;
    public Category? Category { get; set; }
    public AppUser? AssignedUser { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public ICollection<StockCountItem> Items { get; set; } = [];
}
