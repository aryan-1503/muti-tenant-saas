namespace InventoryManager.Domain.Common;

/// <summary>
/// Inherits from BaseEntity and adds TenantId.
/// Every entity that belongs to a specific organisation (tenant) inherits this.
/// EF Core applies a global query filter on TenantId — so queries automatically
/// only return rows matching the current request's tenant. No developer forgets to filter.
/// </summary>
public abstract class TenantScopedEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
