using InventoryManager.Application.Common.Interfaces;

namespace InventoryManager.API.Middleware;

/// <summary>
/// Runs after UseAuthentication() and UseAuthorization().
/// Reads the tenantId claim from the validated JWT and sets it on ICurrentTenantService.
/// This makes the tenant available to AppDbContext (via the scoped service) for all
/// subsequent query filter evaluation.
///
/// WHY MIDDLEWARE vs JUST USING THE CLAIM IN DbContext?
/// ICurrentTenantService is a scoped service, so it exists for exactly one request.
/// Setting it here ensures the tenant is known BEFORE any controller or handler runs.
/// The AppDbContext (also scoped) picks it up at query time via constructor injection.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService tenantService)
    {
        // Only try to resolve tenant for authenticated requests
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                tenantService.SetTenant(tenantId);
            }
        }

        await _next(context);
    }
}
