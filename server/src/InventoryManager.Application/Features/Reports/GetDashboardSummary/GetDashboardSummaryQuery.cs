using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Reports.GetDashboardSummary;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>
/// The main KPI dashboard — one call returns everything the home page needs.
/// All figures are scoped to the current tenant via global query filters.
/// </summary>
public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

// ─── DTO ─────────────────────────────────────────────────────────────────────
public record DashboardSummaryDto(
    // Stock health
    int TotalActiveProducts,
    int TotalActiveWarehouses,
    int LowStockItems,          // Products at or below their ReorderPoint
    int OutOfStockItems,        // Products with zero stock anywhere
    decimal TotalStockValue,    // Sum of (QuantityOnHand × CostPrice) across all warehouses

    // Procurement
    int OpenPurchaseOrders,     // POs that are not Closed/Cancelled/FullyReceived
    decimal PendingOrderValue,  // Total value of open POs

    // Transfers
    int InTransitTransfers,     // Transfers currently InTransit

    // Counts
    int PendingStockCounts,     // Counts in Created/InProgress/Submitted/Reviewing

    // Notifications
    int UnreadNotifications,

    // Recent activity
    List<RecentMovementDto> RecentMovements,
    List<TopLowStockItemDto> TopLowStockItems
);

public record RecentMovementDto(
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string WarehouseName,
    MovementType MovementType,
    decimal QuantityChange,
    DateTime MovedAt
);

public record TopLowStockItemDto(
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string? CategoryName,
    decimal TotalOnHand,
    decimal ReorderPoint,
    decimal MinStockLevel,
    string StockStatus
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardSummaryQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        // ── Core counts ───────────────────────────────────────────────────────
        var totalProducts = await _db.Products.CountAsync(p => p.IsActive, cancellationToken);
        var totalWarehouses = await _db.Warehouses.CountAsync(w => w.IsActive, cancellationToken);

        // ── Stock health ──────────────────────────────────────────────────────
        // Aggregate per-product total stock across all warehouses
        var productStockTotals = await _db.StockLevels
            .Where(sl => sl.Product.IsActive && sl.Warehouse.IsActive)
            .GroupBy(sl => new
            {
                sl.ProductId,
                sl.Product.ReorderPoint,
                sl.Product.MinStockLevel,
                sl.Product.CostPrice
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ReorderPoint,
                g.Key.MinStockLevel,
                g.Key.CostPrice,
                TotalOnHand = g.Sum(sl => sl.QuantityOnHand)
            })
            .ToListAsync(cancellationToken);

        var lowStockItems = productStockTotals.Count(p => p.TotalOnHand <= p.ReorderPoint && p.TotalOnHand > 0);
        var outOfStockItems = productStockTotals.Count(p => p.TotalOnHand == 0);
        var totalStockValue = productStockTotals.Sum(p => p.TotalOnHand * p.CostPrice);

        // ── Procurement ───────────────────────────────────────────────────────
        var openPoStatuses = new[]
        {
            PurchaseOrderStatus.Draft, PurchaseOrderStatus.Sent,
            PurchaseOrderStatus.Confirmed, PurchaseOrderStatus.PartiallyReceived
        };

        var openPos = await _db.PurchaseOrders
            .Include(po => po.Lines)
            .Where(po => openPoStatuses.Contains(po.Status))
            .ToListAsync(cancellationToken);

        var openPoCount = openPos.Count;
        var pendingOrderValue = openPos.Sum(po => po.Lines.Sum(l => l.QuantityOrdered * l.UnitCost));

        // ── Transfers ─────────────────────────────────────────────────────────
        var inTransitCount = await _db.StockTransfers
            .CountAsync(t => t.Status == TransferStatus.InTransit, cancellationToken);

        // ── Stock counts ──────────────────────────────────────────────────────
        var pendingCountStatuses = new[]
        {
            StockCountStatus.Created, StockCountStatus.InProgress,
            StockCountStatus.Submitted, StockCountStatus.Reviewing
        };
        var pendingCounts = await _db.StockCounts
            .CountAsync(sc => pendingCountStatuses.Contains(sc.Status), cancellationToken);

        // ── Notifications ─────────────────────────────────────────────────────
        var unreadNotifications = await _db.Notifications
            .CountAsync(n => n.UserId == _currentUser.UserId && !n.IsRead, cancellationToken);

        // ── Recent movements (last 10) ────────────────────────────────────────
        var recentMovements = await _db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.Warehouse)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .Select(m => new RecentMovementDto(
                m.ProductId,
                m.Product.Name,
                m.Product.SkuCode,
                m.Warehouse.Name,
                m.MovementType,
                m.QuantityChange,
                m.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        // ── Top low-stock items (worst 10) ────────────────────────────────────
        var topLowStock = await _db.StockLevels
            .Where(sl => sl.Product.IsActive && sl.Warehouse.IsActive)
            .GroupBy(sl => new
            {
                sl.ProductId,
                sl.Product.Name,
                sl.Product.SkuCode,
                sl.Product.ReorderPoint,
                sl.Product.MinStockLevel,
                CategoryName = sl.Product.Category != null ? sl.Product.Category.Name : null
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                g.Key.SkuCode,
                g.Key.CategoryName,
                g.Key.ReorderPoint,
                g.Key.MinStockLevel,
                TotalOnHand = g.Sum(sl => sl.QuantityOnHand)
            })
            .Where(p => p.TotalOnHand <= p.ReorderPoint)
            .OrderBy(p => p.TotalOnHand / (p.ReorderPoint == 0 ? 1 : p.ReorderPoint))  // Worst ratio first
            .Take(10)
            .ToListAsync(cancellationToken);

        var topLowStockDtos = topLowStock.Select(p =>
        {
            var status = p.TotalOnHand == 0 ? "OutOfStock"
                : p.TotalOnHand <= p.MinStockLevel ? "Critical"
                : "LowStock";
            return new TopLowStockItemDto(
                p.ProductId, p.Name, p.SkuCode, p.CategoryName,
                p.TotalOnHand, p.ReorderPoint, p.MinStockLevel, status);
        }).ToList();

        return new DashboardSummaryDto(
            totalProducts,
            totalWarehouses,
            lowStockItems,
            outOfStockItems,
            totalStockValue,
            openPoCount,
            pendingOrderValue,
            inTransitCount,
            pendingCounts,
            unreadNotifications,
            recentMovements,
            topLowStockDtos
        );
    }
}
