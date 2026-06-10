using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A product (SKU) in the catalogue. This is a template — actual stock quantities
/// live in StockLevel (one row per product+warehouse combination).
/// ReorderPoint drives the low-stock alert system.
/// </summary>
public class Product : TenantScopedEntity
{
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SkuCode { get; set; } = string.Empty;    // Unique within a tenant
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string UnitOfMeasure { get; set; } = "Each";   // Each, Box, Kg, Litre, etc.
    public decimal CostPrice { get; set; }                  // What you pay the supplier
    public decimal? SellPrice { get; set; }                 // What you charge the customer
    public decimal ReorderPoint { get; set; }               // Alert triggers at or below this
    public decimal ReorderQuantity { get; set; }            // Suggested qty to order
    public decimal MinStockLevel { get; set; }              // Hard floor — never go below this
    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Category? Category { get; set; }
    public ICollection<StockLevel> StockLevels { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
}
