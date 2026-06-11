using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Stock.GetStockLevels;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Returns a product × warehouse matrix of current stock levels.
/// This is the "stock dashboard" view.
///
/// Filters:
/// - WarehouseId: only one warehouse
/// - ProductId: only one product across all warehouses
/// - BelowReorderPoint: the "needs reordering" dashboard filter
/// </summary>
public record GetStockLevelsQuery(
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? CategoryId = null,
    bool BelowReorderPoint = false
) : IRequest<List<StockLevelDto>>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record StockLevelDto(
    Guid StockLevelId,
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string? CategoryName,
    string UnitOfMeasure,
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal QuantityInTransit,
    decimal ReorderPoint,
    decimal MinStockLevel,
    StockStatus Status,
    DateTime? LastMovementAt
);

public enum StockStatus
{
    Healthy,        // Above reorder point
    LowStock,       // At or below reorder point but above min
    Critical,       // At or below min stock level
    OutOfStock      // Zero
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetStockLevelsQueryHandler : IRequestHandler<GetStockLevelsQuery, List<StockLevelDto>>
{
    private readonly IAppDbContext _db;

    public GetStockLevelsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<StockLevelDto>> Handle(
        GetStockLevelsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockLevels
            .Include(sl => sl.Product).ThenInclude(p => p.Category)
            .Include(sl => sl.Warehouse)
            .Where(sl => sl.Warehouse.IsActive && sl.Product.IsActive)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(sl => sl.WarehouseId == request.WarehouseId);

        if (request.ProductId.HasValue)
            query = query.Where(sl => sl.ProductId == request.ProductId);

        if (request.CategoryId.HasValue)
            query = query.Where(sl => sl.Product.CategoryId == request.CategoryId);

        if (request.BelowReorderPoint)
            query = query.Where(sl => sl.QuantityOnHand <= sl.Product.ReorderPoint);

        var levels = await query
            .OrderBy(sl => sl.Product.Name)
            .ThenBy(sl => sl.Warehouse.Name)
            .ToListAsync(cancellationToken);

        // Load last movement date for each stock level (separate query is more efficient
        // than joining StockMovements inline for large datasets)
        var productWarehousePairs = levels
            .Select(sl => new { sl.ProductId, sl.WarehouseId })
            .ToList();

        var lastMovements = await _db.StockMovements
            .Where(m => productWarehousePairs
                .Any(p => p.ProductId == m.ProductId && p.WarehouseId == m.WarehouseId))
            .GroupBy(m => new { m.ProductId, m.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                LastAt = g.Max(m => m.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        return levels.Select(sl =>
        {
            var last = lastMovements
                .FirstOrDefault(lm => lm.ProductId == sl.ProductId && lm.WarehouseId == sl.WarehouseId);

            var status = sl.QuantityOnHand == 0 ? StockStatus.OutOfStock
                : sl.QuantityOnHand <= sl.Product.MinStockLevel ? StockStatus.Critical
                : sl.QuantityOnHand <= sl.Product.ReorderPoint ? StockStatus.LowStock
                : StockStatus.Healthy;

            return new StockLevelDto(
                sl.Id,
                sl.ProductId,
                sl.Product.Name,
                sl.Product.SkuCode,
                sl.Product.Category?.Name,
                sl.Product.UnitOfMeasure,
                sl.WarehouseId,
                sl.Warehouse.Name,
                sl.QuantityOnHand,
                sl.QuantityInTransit,
                sl.Product.ReorderPoint,
                sl.Product.MinStockLevel,
                status,
                last?.LastAt
            );
        }).ToList();
    }
}
