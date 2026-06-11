using InventoryManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Common.Interfaces;

/// <summary>
/// Interface for AppDbContext exposed to the Application layer.
/// This keeps Application from depending on EF Core directly (only Infrastructure knows EF).
/// All handlers use this interface — allowing easy in-memory fakes for unit tests.
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<AppUser> Users { get; }
    DbSet<InviteToken> InviteTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<StockLevel> StockLevels { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockTransferLine> StockTransferLines { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; }
    DbSet<GoodsReceiptNoteLine> GoodsReceiptNoteLines { get; }
    DbSet<StockCount> StockCounts { get; }
    DbSet<StockCountItem> StockCountItems { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
