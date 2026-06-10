using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A company that supplies goods. Maintained per-tenant.
/// Used to auto-fill contact details when creating purchase orders.
/// </summary>
public class Supplier : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int? LeadTimeDays { get; set; }       // Average days from order to delivery
    public string? PaymentTerms { get; set; }    // e.g. "Net 30", "COD"
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}
