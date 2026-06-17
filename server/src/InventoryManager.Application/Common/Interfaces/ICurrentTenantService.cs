namespace InventoryManager.Application.Common.Interfaces;

/// <summary>
/// Provides the current tenant's ID for the duration of the HTTP request.
/// Implemented in Infrastructure and set by TenantResolutionMiddleware from the JWT claim.
/// Used by AppDbContext to apply global query filters automatically.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// Returns the resolved tenant ID, or Guid.Empty if no tenant has been set.
    /// Safe to use in EF query filters — they evaluate lazily at query time.
    /// Use HasTenant to check if a tenant is present before relying on this value
    /// in business logic.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// True if a tenant has been resolved for the current request (e.g. authenticated endpoints).
    /// False for unauthenticated flows like registration or password reset.
    /// </summary>
    bool HasTenant { get; }

    void SetTenant(Guid tenantId);
}
