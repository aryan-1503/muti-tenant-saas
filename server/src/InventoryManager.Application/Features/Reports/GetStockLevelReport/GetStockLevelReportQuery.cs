using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Reports.GetStockLevelReport;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Full stock level report — the product × warehouse matrix with full valuation.
/// Filterable for focused views. Export-ready (no paging — returns all rows).
/// </summary>
public record GetStockLevelReportQuery(
    Guid? WarehouseId = null,
    Guid? CategoryId = null
) : IRequest<StockLevelReportDto>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record StockLevelReportDto(
    DateTime GeneratedAt,
    StockLevelReportTotals Totals,
    List<StockLevelReportRow> Rows
);

public record StockLevelReportTotals(
    int ProductCount,
    int WarehouseCount,
    decimal TotalQuantityOnHand,
    decimal TotalStockValue,       // Sum of (QtyOnHand × CostPrice)
    decimal TotalReorderValue      // Value of what's needed to reach ReorderPoint
);

public record StockLevelReportRow(
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string? CategoryName,
    string UnitOfMeasure,
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal QuantityInTransit,
    decimal CostPrice,
    decimal? SellPrice,
    decimal StockValue,            // QuantityOnHand × CostPrice
    decimal ReorderPoint,
    decimal MinStockLevel,
    string Status                  // Healthy / LowStock / Critical / OutOfStock
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetStockLevelReportQueryHandler
    : IRequestHandler<GetStockLevelReportQuery, StockLevelReportDto>
{
    private readonly IAppDbContext _db;
    public GetStockLevelReportQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StockLevelReportDto> Handle(
        GetStockLevelReportQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockLevels
            .Include(sl => sl.Product).ThenInclude(p => p.Category)
            .Include(sl => sl.Warehouse)
            .Where(sl => sl.Product.IsActive && sl.Warehouse.IsActive)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(sl => sl.WarehouseId == request.WarehouseId);
        if (request.CategoryId.HasValue)
            query = query.Where(sl => sl.Product.CategoryId == request.CategoryId);

        var levels = await query
            .OrderBy(sl => sl.Product.Name)
            .ThenBy(sl => sl.Warehouse.Name)
            .ToListAsync(cancellationToken);

        var rows = levels.Select(sl =>
        {
            var status = sl.QuantityOnHand == 0 ? "OutOfStock"
                : sl.QuantityOnHand <= sl.Product.MinStockLevel ? "Critical"
                : sl.QuantityOnHand <= sl.Product.ReorderPoint ? "LowStock"
                : "Healthy";

            return new StockLevelReportRow(
                sl.ProductId,
                sl.Product.Name,
                sl.Product.SkuCode,
                sl.Product.Category?.Name,
                sl.Product.UnitOfMeasure,
                sl.WarehouseId,
                sl.Warehouse.Name,
                sl.QuantityOnHand,
                sl.QuantityInTransit,
                sl.Product.CostPrice,
                sl.Product.SellPrice,
                sl.QuantityOnHand * sl.Product.CostPrice,
                sl.Product.ReorderPoint,
                sl.Product.MinStockLevel,
                status
            );
        }).ToList();

        var totals = new StockLevelReportTotals(
            ProductCount: levels.Select(sl => sl.ProductId).Distinct().Count(),
            WarehouseCount: levels.Select(sl => sl.WarehouseId).Distinct().Count(),
            TotalQuantityOnHand: levels.Sum(sl => sl.QuantityOnHand),
            TotalStockValue: rows.Sum(r => r.StockValue),
            TotalReorderValue: levels
                .Where(sl => sl.QuantityOnHand < sl.Product.ReorderPoint)
                .Sum(sl => (sl.Product.ReorderPoint - sl.QuantityOnHand) * sl.Product.CostPrice)
        );

        return new StockLevelReportDto(DateTime.UtcNow, totals, rows);
    }
}
