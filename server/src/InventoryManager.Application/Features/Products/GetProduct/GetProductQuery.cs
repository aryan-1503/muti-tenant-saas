using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Products.GetProduct;

// ─── Query ────────────────────────────────────────────────────────────────────
public record GetProductQuery(Guid Id) : IRequest<ProductDetailDto>;

// ─── DTO ─────────────────────────────────────────────────────────────────────
public record ProductDetailDto(
    Guid Id,
    string Name,
    string SkuCode,
    string? Description,
    string? Barcode,
    Guid? CategoryId,
    string? CategoryName,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal? SellPrice,
    decimal ReorderPoint,
    decimal ReorderQuantity,
    decimal MinStockLevel,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<StockByWarehouseDto> StockByWarehouse
);

public record StockByWarehouseDto(
    Guid WarehouseId,
    string WarehouseName,
    decimal QuantityOnHand,
    decimal QuantityInTransit
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDetailDto>
{
    private readonly IAppDbContext _db;

    public GetProductQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ProductDetailDto> Handle(
        GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.StockLevels)
                .ThenInclude(sl => sl.Warehouse)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        return new ProductDetailDto(
            product.Id,
            product.Name,
            product.SkuCode,
            product.Description,
            product.Barcode,
            product.CategoryId,
            product.Category?.Name,
            product.UnitOfMeasure,
            product.CostPrice,
            product.SellPrice,
            product.ReorderPoint,
            product.ReorderQuantity,
            product.MinStockLevel,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt,
            product.StockLevels
                .Select(sl => new StockByWarehouseDto(
                    sl.WarehouseId,
                    sl.Warehouse.Name,
                    sl.QuantityOnHand,
                    sl.QuantityInTransit))
                .ToList()
        );
    }
}
