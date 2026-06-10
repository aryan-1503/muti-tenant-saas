namespace InventoryManager.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Provides Id (GUID) and audit timestamps.
/// TenantId is not here — it's on tenant-scoped entities only (Tenant itself doesn't have TenantId).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
