using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.PurchaseOrders.GetPurchaseOrder;

public record GetPurchaseOrderQuery(Guid Id) : IRequest<PurchaseOrderDetailDto>;

public record PurchaseOrderDetailDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string? SupplierEmail,
    int? SupplierLeadTimeDays,
    Guid WarehouseId,
    string WarehouseName,
    PurchaseOrderStatus Status,
    string StatusLabel,
    DateTime? ExpectedDeliveryDate,
    string? Notes,
    decimal TotalValue,
    string CreatedByUserName,
    DateTime CreatedAt,
    List<PurchaseOrderLineDetailDto> Lines,
    List<GrnSummaryDto> GoodsReceiptNotes
);

public record PurchaseOrderLineDetailDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string UnitOfMeasure,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal QuantityOutstanding,
    decimal UnitCost,
    decimal LineTotal
);

public record GrnSummaryDto(
    Guid Id,
    DateTime ReceivedAt,
    string ReceivedByUserName,
    string WarehouseName,
    int LineCount
);

public class GetPurchaseOrderQueryHandler : IRequestHandler<GetPurchaseOrderQuery, PurchaseOrderDetailDto>
{
    private readonly IAppDbContext _db;
    public GetPurchaseOrderQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PurchaseOrderDetailDto> Handle(
        GetPurchaseOrderQuery request, CancellationToken cancellationToken)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.CreatedByUser)
            .Include(p => p.Lines).ThenInclude(l => l.Product)
            .Include(p => p.GoodsReceiptNotes).ThenInclude(g => g.ReceivedByUser)
            .Include(p => p.GoodsReceiptNotes).ThenInclude(g => g.Warehouse)
            .Include(p => p.GoodsReceiptNotes).ThenInclude(g => g.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order not found.");

        return new PurchaseOrderDetailDto(
            po.Id,
            po.SupplierId,
            po.Supplier.Name,
            po.Supplier.Email,
            po.Supplier.LeadTimeDays,
            po.WarehouseId,
            po.Warehouse.Name,
            po.Status,
            po.Status.ToString(),
            po.ExpectedDeliveryDate,
            po.Notes,
            po.Lines.Sum(l => l.QuantityOrdered * l.UnitCost),
            po.CreatedByUser.FullName,
            po.CreatedAt,
            po.Lines.Select(l => new PurchaseOrderLineDetailDto(
                l.Id,
                l.ProductId,
                l.Product.Name,
                l.Product.SkuCode,
                l.Product.UnitOfMeasure,
                l.QuantityOrdered,
                l.QuantityReceived,
                l.QuantityOrdered - l.QuantityReceived,
                l.UnitCost,
                l.QuantityOrdered * l.UnitCost
            )).ToList(),
            po.GoodsReceiptNotes.Select(g => new GrnSummaryDto(
                g.Id,
                g.ReceivedAt,
                g.ReceivedByUser.FullName,
                g.Warehouse.Name,
                g.Lines.Count
            )).OrderByDescending(g => g.ReceivedAt).ToList()
        );
    }
}
