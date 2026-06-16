using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Reports.GetMovementHistoryReport;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Full movement history report — export-ready, date-ranged.
/// Summarises stock in vs stock out by product and movement type.
/// Useful for: period-end stock reconciliation, shrinkage analysis.
/// </summary>
public record GetMovementHistoryReportQuery(
    DateTime From,
    DateTime To,
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? CategoryId = null
) : IRequest<MovementHistoryReportDto>;

public record MovementHistoryReportDto(
    DateTime From,
    DateTime To,
    DateTime GeneratedAt,
    MovementHistoryReportTotals Totals,
    List<MovementHistoryReportRow> Rows
);

public record MovementHistoryReportTotals(
    decimal TotalStockIn,
    decimal TotalStockOut,
    decimal NetMovement,
    int TotalMovementCount,
    Dictionary<string, decimal> ByMovementType   // e.g. {"GoodsIn": 500, "ManualAdjustment": -20}
);

public record MovementHistoryReportRow(
    DateTime MovedAt,
    string ProductName,
    string SkuCode,
    string WarehouseName,
    MovementType MovementType,
    string MovementTypeLabel,
    decimal QuantityChange,
    decimal QuantityAfter,
    string? Reason,
    string? ReferenceType,
    Guid? ReferenceId,
    string MovedByUser
);

public class GetMovementHistoryReportQueryHandler
    : IRequestHandler<GetMovementHistoryReportQuery, MovementHistoryReportDto>
{
    private readonly IAppDbContext _db;
    public GetMovementHistoryReportQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MovementHistoryReportDto> Handle(
        GetMovementHistoryReportQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToUniversalTime();
        var to = request.To.ToUniversalTime();

        if (from > to)
            throw new ArgumentException("From date must be before To date.");
        if ((to - from).TotalDays > 366)
            throw new ArgumentException("Date range cannot exceed 366 days.");

        var query = _db.StockMovements
            .Include(m => m.Product).ThenInclude(p => p.Category)
            .Include(m => m.Warehouse)
            .Include(m => m.CreatedByUser)
            .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(m => m.WarehouseId == request.WarehouseId);
        if (request.ProductId.HasValue)
            query = query.Where(m => m.ProductId == request.ProductId);
        if (request.CategoryId.HasValue)
            query = query.Where(m => m.Product.CategoryId == request.CategoryId);

        var movements = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var rows = movements.Select(m => new MovementHistoryReportRow(
            m.CreatedAt,
            m.Product.Name,
            m.Product.SkuCode,
            m.Warehouse.Name,
            m.MovementType,
            m.MovementType.ToString(),
            m.QuantityChange,
            m.QuantityAfter,
            m.Reason,
            m.ReferenceType,
            m.ReferenceId,
            m.CreatedByUser.FullName
        )).ToList();

        var byType = movements
            .GroupBy(m => m.MovementType.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(m => m.QuantityChange));

        var totalIn = movements.Where(m => m.QuantityChange > 0).Sum(m => m.QuantityChange);
        var totalOut = Math.Abs(movements.Where(m => m.QuantityChange < 0).Sum(m => m.QuantityChange));

        return new MovementHistoryReportDto(
            request.From, request.To, DateTime.UtcNow,
            new MovementHistoryReportTotals(totalIn, totalOut, totalIn - totalOut, movements.Count, byType),
            rows
        );
    }
}
