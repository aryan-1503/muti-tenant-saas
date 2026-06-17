using InventoryManager.Application.Common.Interfaces;

namespace InventoryManager.Infrastructure.Services;

/// <summary>
/// Scoped service — lives for the duration of one HTTP request.
/// Populated by TenantResolutionMiddleware after the JWT is validated.
/// All EF Core queries use this to apply the global tenant filter.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private Guid _tenantId;

    public bool HasTenant => _tenantId != Guid.Empty;

    /// <summary>
    /// Returns the current tenant ID, or Guid.Empty if not yet resolved.
    /// Safe to use in EF query filters (evaluated at query time).
    /// For controllers/handlers that require a tenant, check HasTenant first
    /// or use the [Authorize] attribute to ensure the middleware has run.
    /// </summary>
    public Guid TenantId => _tenantId;

    public void SetTenant(Guid tenantId) => _tenantId = tenantId;
}
