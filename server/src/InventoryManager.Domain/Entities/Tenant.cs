using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// Represents a customer organisation (a SaaS tenant).
/// This is the root isolation boundary — all other data belongs to a Tenant.
/// Tenant itself does NOT have a TenantId (it IS the tenant).
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string CurrencyCode { get; set; } = "USD";  // ISO 4217
    public string TimezoneId { get; set; } = "UTC";     // IANA timezone id
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<Warehouse> Warehouses { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
