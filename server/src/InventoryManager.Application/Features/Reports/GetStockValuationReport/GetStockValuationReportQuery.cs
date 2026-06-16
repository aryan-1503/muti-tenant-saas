using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Reports.GetStockValuationReport;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Inventory valuation report — the financial view of stock.
/// Groups by Category and Warehouse to show where value is concentrated.
/// Useful for: balance sheet inventory line, period-end reporting, insurance.
/// </summary>
public record GetStockValuationReportQuery(
    Guid? WarehouseId = null,
    Guid? CategoryId = null
) : IRequest<StockValuationReportDto>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record StockValuationReportDto(
    DateTime GeneratedAt,
    decimal GrandTotalCostValue,
    decimal GrandTotalSellValue,
    decimal PotentialGrossProfit,
    List<ValuationByCategory> ByCategory,
    List<ValuationByWarehouse> ByWarehouse
);

public record ValuationByCategory(
    string CategoryName,
    int ProductCount,
    decimal TotalQuantity,
    decimal TotalCostValue,
    decimal TotalSellValue
);

public record ValuationByWarehouse(
    string WarehouseName,
    int ProductCount,
    decimal TotalQuantity,
    decimal TotalCostValue,
    decimal TotalSellValue
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetStockValuationReportQueryHandler
    : IRequestHandler<GetStockValuationReportQuery, StockValuationReportDto>
{
    private readonly IAppDbContext _db;
    public GetStockValuationReportQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StockValuationReportDto> Handle(
        GetStockValuationReportQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockLevels
            .Include(sl => sl.Product).ThenInclude(p => p.Category)
            .Include(sl => sl.Warehouse)
            .Where(sl => sl.Product.IsActive && sl.Warehouse.IsActive && sl.QuantityOnHand > 0)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(sl => sl.WarehouseId == request.WarehouseId);
        if (request.CategoryId.HasValue)
            query = query.Where(sl => sl.Product.CategoryId == request.CategoryId);

        var levels = await query.ToListAsync(cancellationToken);

        // By category
        var byCategory = levels
            .GroupBy(sl => sl.Product.Category?.Name ?? "Uncategorised")
            .Select(g => new ValuationByCategory(
                g.Key,
                g.Select(sl => sl.ProductId).Distinct().Count(),
                g.Sum(sl => sl.QuantityOnHand),
                g.Sum(sl => sl.QuantityOnHand * sl.Product.CostPrice),
                g.Sum(sl => sl.QuantityOnHand * (sl.Product.SellPrice ?? sl.Product.CostPrice))
            ))
            .OrderByDescending(c => c.TotalCostValue)
            .ToList();

        // By warehouse
        var byWarehouse = levels
            .GroupBy(sl => sl.Warehouse.Name)
            .Select(g => new ValuationByWarehouse(
                g.Key,
                g.Select(sl => sl.ProductId).Distinct().Count(),
                g.Sum(sl => sl.QuantityOnHand),
                g.Sum(sl => sl.QuantityOnHand * sl.Product.CostPrice),
                g.Sum(sl => sl.QuantityOnHand * (sl.Product.SellPrice ?? sl.Product.CostPrice))
            ))
            .OrderByDescending(w => w.TotalCostValue)
            .ToList();

        var grandCost = levels.Sum(sl => sl.QuantityOnHand * sl.Product.CostPrice);
        var grandSell = levels.Sum(sl => sl.QuantityOnHand * (sl.Product.SellPrice ?? sl.Product.CostPrice));

        return new StockValuationReportDto(
            DateTime.UtcNow,
            grandCost,
            grandSell,
            grandSell - grandCost,
            byCategory,
            byWarehouse
        );
    }
}
