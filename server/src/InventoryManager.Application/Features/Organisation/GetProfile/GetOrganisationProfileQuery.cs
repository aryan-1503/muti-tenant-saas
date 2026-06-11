using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Organisation.GetProfile;

// ─── Query ────────────────────────────────────────────────────────────────────
public record GetOrganisationProfileQuery : IRequest<OrganisationProfileDto>;

// ─── DTO ─────────────────────────────────────────────────────────────────────
public record OrganisationProfileDto(
    Guid Id,
    string Name,
    string? Industry,
    string CurrencyCode,
    string TimezoneId,
    string? LogoUrl,
    bool IsActive,
    DateTime CreatedAt,
    int TotalUsers,
    int TotalWarehouses
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetOrganisationProfileQueryHandler
    : IRequestHandler<GetOrganisationProfileQuery, OrganisationProfileDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetOrganisationProfileQueryHandler(IAppDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<OrganisationProfileDto> Handle(
        GetOrganisationProfileQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId;

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()   // Tenant has no TenantId filter — it IS the tenant
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Organisation not found.");

        var userCount = await _db.Users.CountAsync(cancellationToken);
        var warehouseCount = await _db.Warehouses
            .CountAsync(w => w.IsActive, cancellationToken);

        return new OrganisationProfileDto(
            tenant.Id,
            tenant.Name,
            tenant.Industry,
            tenant.CurrencyCode,
            tenant.TimezoneId,
            tenant.LogoUrl,
            tenant.IsActive,
            tenant.CreatedAt,
            userCount,
            warehouseCount
        );
    }
}
