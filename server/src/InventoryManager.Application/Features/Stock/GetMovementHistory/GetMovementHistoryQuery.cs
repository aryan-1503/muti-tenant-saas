using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Stock.GetMovementHistory;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// Full paginated audit trail of all stock movements.
/// This is the "what happened and when" view — read by all roles.
/// Filterable by product, warehouse, movement type, and date range.
/// </summary>
public record GetMovementHistoryQuery(
    Guid? ProductId = null,
    Guid? WarehouseId = null,
    MovementType? MovementType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<MovementHistoryResult>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record MovementHistoryResult(
    List<MovementDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record MovementDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string SkuCode,
    Guid WarehouseId,
    string WarehouseName,
    MovementType MovementType,
    string MovementTypeLabel,
    decimal QuantityChange,
    decimal QuantityAfter,
    string? Reason,
    string? Notes,
    string? ReferenceType,
    Guid? ReferenceId,
    string CreatedByUserName,
    DateTime CreatedAt
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetMovementHistoryQueryHandler
    : IRequestHandler<GetMovementHistoryQuery, MovementHistoryResult>
{
    private readonly IAppDbContext _db;

    public GetMovementHistoryQueryHandler(IAppDbContext db) => _db = db;

    public async Task<MovementHistoryResult> Handle(
        GetMovementHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Warehouse)
            .Include(m => m.CreatedByUser)
            .AsQueryable();

        if (request.ProductId.HasValue)
            query = query.Where(m => m.ProductId == request.ProductId);

        if (request.WarehouseId.HasValue)
            query = query.Where(m => m.WarehouseId == request.WarehouseId);

        if (request.MovementType.HasValue)
            query = query.Where(m => m.MovementType == request.MovementType);

        if (request.From.HasValue)
            query = query.Where(m => m.CreatedAt >= request.From.Value.ToUniversalTime());

        if (request.To.HasValue)
            query = query.Where(m => m.CreatedAt <= request.To.Value.ToUniversalTime());

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var page = Math.Max(1, request.Page);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MovementDto(
                m.Id,
                m.ProductId,
                m.Product.Name,
                m.Product.SkuCode,
                m.WarehouseId,
                m.Warehouse.Name,
                m.MovementType,
                m.MovementType.ToString(),
                m.QuantityChange,
                m.QuantityAfter,
                m.Reason,
                m.Notes,
                m.ReferenceType,
                m.ReferenceId,
                m.CreatedByUser.FullName,
                m.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new MovementHistoryResult(
            items,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );
    }
}
