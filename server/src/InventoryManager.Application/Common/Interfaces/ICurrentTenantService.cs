namespace InventoryManager.Application.Common.Interfaces;

/// <summary>
/// Provides the current tenant's ID for the duration of the HTTP request.
/// Implemented in Infrastructure and set by TenantResolutionMiddleware from the JWT claim.
/// Used by AppDbContext to apply global query filters automatically.
/// </summary>
public interface ICurrentTenantService
{
    Guid TenantId { get; }
    void SetTenant(Guid tenantId);
}
