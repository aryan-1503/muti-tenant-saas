using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A physical location where stock is held.
/// Can be a warehouse, retail store, van, or any storage point.
/// Multi-location support is a core differentiator of this SaaS.
/// </summary>
public class Warehouse : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Type { get; set; }  // e.g. "Warehouse", "Retail Store", "Van"
    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<StockLevel> StockLevels { get; set; } = [];
}
