using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Transfers.ListTransfers;

// ─── Query ────────────────────────────────────────────────────────────────────
public record ListTransfersQuery(
    TransferStatus? Status = null,
    Guid? WarehouseId = null,    // filter by source OR destination warehouse
    int Page = 1,
    int PageSize = 25
) : IRequest<TransferListResult>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record TransferListResult(
    List<TransferSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record TransferSummaryDto(
    Guid Id,
    string FromWarehouseName,
    string ToWarehouseName,
    TransferStatus Status,
    string StatusLabel,
    int LineCount,
    string CreatedByUserName,
    DateTime CreatedAt,
    DateTime? DispatchedAt,
    DateTime? CompletedAt
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ListTransfersQueryHandler : IRequestHandler<ListTransfersQuery, TransferListResult>
{
    private readonly IAppDbContext _db;

    public ListTransfersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TransferListResult> Handle(
        ListTransfersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.StockTransfers
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.CreatedByUser)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(t => t.Status == request.Status);

        if (request.WarehouseId.HasValue)
            query = query.Where(t =>
                t.FromWarehouseId == request.WarehouseId ||
                t.ToWarehouseId == request.WarehouseId);

        var total = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransferSummaryDto(
                t.Id,
                t.FromWarehouse.Name,
                t.ToWarehouse.Name,
                t.Status,
                t.Status.ToString(),
                t.Lines.Count,
                t.CreatedByUser.FullName,
                t.CreatedAt,
                t.DispatchedAt,
                t.CompletedAt
            ))
            .ToListAsync(cancellationToken);

        return new TransferListResult(items, total, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize));
    }
}
