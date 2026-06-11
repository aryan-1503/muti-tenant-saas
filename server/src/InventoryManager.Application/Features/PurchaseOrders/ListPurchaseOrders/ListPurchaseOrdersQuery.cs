using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.PurchaseOrders.ListPurchaseOrders;

public record ListPurchaseOrdersQuery(
    PurchaseOrderStatus? Status = null,
    Guid? SupplierId = null,
    Guid? WarehouseId = null,
    int Page = 1,
    int PageSize = 25
) : IRequest<PurchaseOrderListResult>;

public record PurchaseOrderListResult(
    List<PurchaseOrderSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record PurchaseOrderSummaryDto(
    Guid Id,
    string SupplierName,
    string WarehouseName,
    PurchaseOrderStatus Status,
    string StatusLabel,
    decimal TotalValue,
    int LineCount,
    DateTime? ExpectedDeliveryDate,
    string CreatedByUserName,
    DateTime CreatedAt
);

public class ListPurchaseOrdersQueryHandler
    : IRequestHandler<ListPurchaseOrdersQuery, PurchaseOrderListResult>
{
    private readonly IAppDbContext _db;
    public ListPurchaseOrdersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PurchaseOrderListResult> Handle(
        ListPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .Include(po => po.CreatedByUser)
            .AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(po => po.Status == request.Status);
        if (request.SupplierId.HasValue)
            query = query.Where(po => po.SupplierId == request.SupplierId);
        if (request.WarehouseId.HasValue)
            query = query.Where(po => po.WarehouseId == request.WarehouseId);

        var total = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        var items = await query
            .OrderByDescending(po => po.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(po => new PurchaseOrderSummaryDto(
                po.Id,
                po.Supplier.Name,
                po.Warehouse.Name,
                po.Status,
                po.Status.ToString(),
                po.Lines.Sum(l => l.QuantityOrdered * l.UnitCost),
                po.Lines.Count,
                po.ExpectedDeliveryDate,
                po.CreatedByUser.FullName,
                po.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PurchaseOrderListResult(items, total, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize));
    }
}
