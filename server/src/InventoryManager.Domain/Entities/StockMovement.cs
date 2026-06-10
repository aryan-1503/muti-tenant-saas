using InventoryManager.Domain.Common;
using InventoryManager.Domain.Enums;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// IMMUTABLE audit log of every stock change. Never updated, never deleted.
/// Every operation that changes stock MUST write here first.
/// This is the single source of truth for "what happened and why".
/// QuantityChange can be positive (in) or negative (out).
/// QuantityAfter captures the running balance at the time of the movement.
/// </summary>
public class StockMovement : TenantScopedEntity
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public MovementType MovementType { get; set; }
    public decimal QuantityChange { get; set; }   // Positive = stock added, Negative = stock removed
    public decimal QuantityAfter { get; set; }    // Running balance snapshot after this movement
    public string? ReferenceType { get; set; }    // "GRN", "Transfer", "StockCount", "PurchaseOrder"
    public Guid? ReferenceId { get; set; }        // FK to the source record
    public string? Reason { get; set; }           // Required for ManualAdjustment
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
}
