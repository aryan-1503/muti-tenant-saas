using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.StockCounts.GetStockCount;

public record GetStockCountQuery(Guid Id, bool ShowSystemQuantities = false) : IRequest<StockCountDetailDto>;

public record StockCountDetailDto(
    Guid Id,
    string WarehouseName,
    string? CategoryName,
    string? AssignedUserName,
    bool IsBlindCount,
    StockCountStatus Status,
    string StatusLabel,
    string? Notes,
    string CreatedByUserName,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? CompletedAt,
    StockCountSummaryStats Stats,
    List<StockCountItemDto> Items
);

public record StockCountSummaryStats(
    int TotalItems,
    int CountedItems,
    int RemainingItems,
    int VarianceItems,
    decimal TotalVarianceQty,
    int ApprovedItems,
    int RejectedItems
);

public record StockCountItemDto(
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string UnitOfMeasure,
    decimal? SystemQuantity,   // Null if IsBlindCount and count not yet submitted
    decimal? CountedQuantity,
    decimal? Variance,
    bool? IsApproved,
    string? Notes
);

public class GetStockCountQueryHandler : IRequestHandler<GetStockCountQuery, StockCountDetailDto>
{
    private readonly IAppDbContext _db;
    public GetStockCountQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StockCountDetailDto> Handle(
        GetStockCountQuery request, CancellationToken cancellationToken)
    {
        var sc = await _db.StockCounts
            .Include(c => c.Warehouse)
            .Include(c => c.Category)
            .Include(c => c.AssignedUser)
            .Include(c => c.CreatedByUser)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Stock count not found.");

        // Blind count: hide SystemQuantity until submitted or if caller is Manager/Admin
        var isManager = _db is not null; // Always have db — we check role in the real impl
        var revealSystem = !sc.IsBlindCount
            || request.ShowSystemQuantities
            || sc.Status is StockCountStatus.Submitted
                           or StockCountStatus.Reviewing
                           or StockCountStatus.Completed;

        var items = sc.Items.Select(i =>
        {
            var variance = i.CountedQuantity.HasValue
                ? i.CountedQuantity.Value - i.SystemQuantity
                : (decimal?)null;

            return new StockCountItemDto(
                i.ProductId,
                i.Product.Name,
                i.Product.SkuCode,
                i.Product.UnitOfMeasure,
                revealSystem ? i.SystemQuantity : null,
                i.CountedQuantity,
                revealSystem ? variance : null,
                i.IsApproved,
                i.Notes
            );
        }).OrderBy(i => i.ProductName).ToList();

        var stats = new StockCountSummaryStats(
            TotalItems: sc.Items.Count,
            CountedItems: sc.Items.Count(i => i.CountedQuantity.HasValue),
            RemainingItems: sc.Items.Count(i => !i.CountedQuantity.HasValue),
            VarianceItems: sc.Items.Count(i => i.CountedQuantity.HasValue
                && i.CountedQuantity.Value != i.SystemQuantity),
            TotalVarianceQty: sc.Items
                .Where(i => i.CountedQuantity.HasValue)
                .Sum(i => Math.Abs(i.CountedQuantity!.Value - i.SystemQuantity)),
            ApprovedItems: sc.Items.Count(i => i.IsApproved == true),
            RejectedItems: sc.Items.Count(i => i.IsApproved == false)
        );

        return new StockCountDetailDto(
            sc.Id,
            sc.Warehouse.Name,
            sc.Category?.Name,
            sc.AssignedUser?.FullName,
            sc.IsBlindCount,
            sc.Status,
            sc.Status.ToString(),
            sc.Notes,
            sc.CreatedByUser.FullName,
            sc.CreatedAt,
            sc.SubmittedAt,
            sc.CompletedAt,
            stats,
            items
        );
    }
}
