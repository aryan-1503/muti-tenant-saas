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

    public Guid TenantId
    {
        get => _tenantId == Guid.Empty
            ? throw new InvalidOperationException("Tenant not resolved. Ensure TenantResolutionMiddleware is registered.")
            : _tenantId;
    }

    public void SetTenant(Guid tenantId) => _tenantId = tenantId;
}
