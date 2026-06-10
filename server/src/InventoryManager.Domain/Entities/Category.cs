using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A grouping of products. Categories are per-tenant and configurable.
/// Used for filtering stock counts and reports.
/// </summary>
public class Category : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Product> Products { get; set; } = [];
}
