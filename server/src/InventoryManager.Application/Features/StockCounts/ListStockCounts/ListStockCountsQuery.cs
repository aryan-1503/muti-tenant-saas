using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.StockCounts.ListStockCounts;

public record ListStockCountsQuery(
    StockCountStatus? Status = null,
    Guid? WarehouseId = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<StockCountListResult>;

public record StockCountListResult(
    List<StockCountSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record StockCountSummaryDto(
    Guid Id,
    string WarehouseName,
    string? CategoryName,
    string? AssignedUserName,
    bool IsBlindCount,
    StockCountStatus Status,
    string StatusLabel,
    int TotalItems,
    int CountedItems,
    int VarianceItems,
    string CreatedByUserName,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? CompletedAt
);

public class ListStockCountsQueryHandler
    : IRequestHandler<ListStockCountsQuery, StockCountListResult>
{
    private readonly IAppDbContext _db;
    public ListStockCountsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StockCountListResult> Handle(
        ListStockCountsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockCounts
            .Include(sc => sc.Warehouse)
            .Include(sc => sc.Category)
            .Include(sc => sc.AssignedUser)
            .Include(sc => sc.CreatedByUser)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(sc => sc.Status == request.Status);
        if (request.WarehouseId.HasValue)
            query = query.Where(sc => sc.WarehouseId == request.WarehouseId);

        var total = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        var items = await query
            .OrderByDescending(sc => sc.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sc => new StockCountSummaryDto(
                sc.Id,
                sc.Warehouse.Name,
                sc.Category != null ? sc.Category.Name : null,
                sc.AssignedUser != null ? sc.AssignedUser.FullName : null,
                sc.IsBlindCount,
                sc.Status,
                sc.Status.ToString(),
                sc.Items.Count,
                sc.Items.Count(i => i.CountedQuantity.HasValue),
                sc.Items.Count(i => i.CountedQuantity.HasValue
                    && i.CountedQuantity.Value != i.SystemQuantity),
                sc.CreatedByUser.FullName,
                sc.CreatedAt,
                sc.SubmittedAt,
                sc.CompletedAt
            ))
            .ToListAsync(cancellationToken);

        return new StockCountListResult(items, total, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize));
    }
}
