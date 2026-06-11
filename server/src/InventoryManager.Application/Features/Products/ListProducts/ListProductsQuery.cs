using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Products.ListProducts;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Paginated, filterable product list.
/// All filters are optional — calling with no params returns all active products.
/// </summary>
public record ListProductsQuery(
    string? Search,           // Name or SKU code free-text search
    Guid? CategoryId,         // Filter by category
    bool? IsActive,           // null = active only, true = active, false = inactive
    int Page = 1,
    int PageSize = 25
) : IRequest<PagedResult<ProductListItemDto>>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record ProductListItemDto(
    Guid Id,
    string Name,
    string SkuCode,
    string? CategoryName,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal? SellPrice,
    decimal ReorderPoint,
    bool IsActive,
    int TotalStockOnHand,
    DateTime CreatedAt
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ListProductsQueryHandler
    : IRequestHandler<ListProductsQuery, PagedResult<ProductListItemDto>>
{
    private readonly IAppDbContext _db;

    public ListProductsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<ProductListItemDto>> Handle(
        ListProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Products.AsQueryable();

        // Active filter — default to active only
        var activeFilter = request.IsActive ?? true;
        query = query.Where(p => p.IsActive == activeFilter);

        // Category filter
        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId);

        // Free-text search — name or SKU
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.SkuCode.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Name,
                p.SkuCode,
                p.Category != null ? p.Category.Name : null,
                p.UnitOfMeasure,
                p.CostPrice,
                p.SellPrice,
                p.ReorderPoint,
                p.IsActive,
                p.StockLevels.Sum(sl => (int)sl.QuantityOnHand),
                p.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItemDto>(
            items,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );
    }
}
