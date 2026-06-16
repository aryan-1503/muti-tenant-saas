using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Reports.GetLowStockReport;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Focused report on items that need attention — at or below reorder point.
/// Aggregates stock across all warehouses per product.
/// Sorted by urgency: OutOfStock → Critical → LowStock.
/// </summary>
public record GetLowStockReportQuery(
    Guid? WarehouseId = null,
    Guid? CategoryId = null,
    bool IncludeCriticalOnly = false   // True = only Critical (at/below MinStockLevel) items
) : IRequest<LowStockReportDto>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record LowStockReportDto(
    DateTime GeneratedAt,
    int OutOfStockCount,
    int CriticalCount,
    int LowStockCount,
    List<LowStockReportRow> Rows
);

public record LowStockReportRow(
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string? CategoryName,
    string UnitOfMeasure,
    decimal TotalOnHand,
    decimal TotalInTransit,
    decimal ReorderPoint,
    decimal ReorderQuantity,
    decimal MinStockLevel,
    decimal ShortfallToReorder,   // How many units needed to hit ReorderPoint
    decimal EstimatedReorderCost, // ShortfallToReorder × CostPrice
    string Status,
    int? SupplierLeadTimeDays,    // From the most recent PO's supplier (if any)
    List<StockByWarehouseRow> ByWarehouse
);

public record StockByWarehouseRow(
    string WarehouseName,
    decimal QuantityOnHand,
    decimal QuantityInTransit
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetLowStockReportQueryHandler
    : IRequestHandler<GetLowStockReportQuery, LowStockReportDto>
{
    private readonly IAppDbContext _db;
    public GetLowStockReportQueryHandler(IAppDbContext db) => _db = db;

    public async Task<LowStockReportDto> Handle(
        GetLowStockReportQuery request, CancellationToken cancellationToken)
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

        var levels = await query.ToListAsync(cancellationToken);

        // Aggregate per product
        var byProduct = levels
            .GroupBy(sl => sl.ProductId)
            .Select(g =>
            {
                var first = g.First();
                var product = first.Product;
                var totalOnHand = g.Sum(sl => sl.QuantityOnHand);
                var totalInTransit = g.Sum(sl => sl.QuantityInTransit);

                return new
                {
                    product.Id,
                    product.Name,
                    product.SkuCode,
                    CategoryName = product.Category?.Name,
                    product.UnitOfMeasure,
                    product.ReorderPoint,
                    product.ReorderQuantity,
                    product.MinStockLevel,
                    product.CostPrice,
                    TotalOnHand = totalOnHand,
                    TotalInTransit = totalInTransit,
                    ByWarehouse = g.Select(sl => new StockByWarehouseRow(
                        sl.Warehouse.Name, sl.QuantityOnHand, sl.QuantityInTransit)).ToList()
                };
            })
            .Where(p => p.TotalOnHand <= p.ReorderPoint)
            .ToList();

        if (request.IncludeCriticalOnly)
            byProduct = byProduct.Where(p => p.TotalOnHand <= p.MinStockLevel).ToList();

        // Load lead times from most recent supplier for each product
        var productIds = byProduct.Select(p => p.Id).ToList();
        var leadTimes = await _db.PurchaseOrderLines
            .Where(pol => productIds.Contains(pol.ProductId))
            .GroupBy(pol => pol.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                LeadTime = g.OrderByDescending(pol => pol.PurchaseOrder.CreatedAt)
                            .Select(pol => pol.PurchaseOrder.Supplier.LeadTimeDays)
                            .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.LeadTime, cancellationToken);

        var rows = byProduct
            .OrderBy(p => p.TotalOnHand == 0 ? 0 : p.TotalOnHand <= p.MinStockLevel ? 1 : 2)
            .ThenBy(p => p.TotalOnHand / (p.ReorderPoint == 0 ? 1 : p.ReorderPoint))
            .Select(p =>
            {
                var status = p.TotalOnHand == 0 ? "OutOfStock"
                    : p.TotalOnHand <= p.MinStockLevel ? "Critical"
                    : "LowStock";

                var shortfall = Math.Max(0, p.ReorderPoint - p.TotalOnHand);
                leadTimes.TryGetValue(p.Id, out var lead);

                return new LowStockReportRow(
                    p.Id, p.Name, p.SkuCode, p.CategoryName, p.UnitOfMeasure,
                    p.TotalOnHand, p.TotalInTransit, p.ReorderPoint, p.ReorderQuantity,
                    p.MinStockLevel, shortfall, shortfall * p.CostPrice, status,
                    lead, p.ByWarehouse
                );
            }).ToList();

        return new LowStockReportDto(
            DateTime.UtcNow,
            rows.Count(r => r.Status == "OutOfStock"),
            rows.Count(r => r.Status == "Critical"),
            rows.Count(r => r.Status == "LowStock"),
            rows
        );
    }
}
