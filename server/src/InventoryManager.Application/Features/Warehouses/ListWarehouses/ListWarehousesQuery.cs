using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Warehouses.ListWarehouses;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>Returns all warehouses for the current tenant, sorted alphabetically.</summary>
public record ListWarehousesQuery(bool IncludeInactive = false) : IRequest<List<WarehouseDto>>;

// ─── DTO ─────────────────────────────────────────────────────────────────────
public record WarehouseDto(
    Guid Id,
    string Name,
    string? Address,
    string? Type,
    bool IsActive,
    int StockLineCount,   // How many distinct products are tracked here
    DateTime CreatedAt
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ListWarehousesQueryHandler : IRequestHandler<ListWarehousesQuery, List<WarehouseDto>>
{
    private readonly IAppDbContext _db;

    public ListWarehousesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<WarehouseDto>> Handle(
        ListWarehousesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Warehouses.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(w => w.IsActive);

        return await query
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Name,
                w.Address,
                w.Type,
                w.IsActive,
                w.StockLevels.Count,
                w.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
