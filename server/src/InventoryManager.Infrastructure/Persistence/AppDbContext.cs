using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Domain.Common;
using InventoryManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core database context.
///
/// Multi-tenancy is enforced here with GLOBAL QUERY FILTERS.
/// Every DbSet for a tenant-scoped entity has a filter applied that restricts
/// queries to the current tenant's rows. This means developers can write:
///
///     context.Products.ToListAsync()
///
/// and ALWAYS get only that tenant's products — no manual .Where(p => p.TenantId == ...) needed.
/// The filter is applied at the database query level, not in-memory.
///
/// HOW TENANCY WORKS:
/// 1. User authenticates → JWT contains tenantId claim
/// 2. TenantResolutionMiddleware reads claim → calls ICurrentTenantService.SetTenant(id)
/// 3. AppDbContext (scoped) reads TenantId from ICurrentTenantService at query time
/// 4. EF Core adds WHERE TenantId = @currentTenantId to every query automatically
///
/// IMPORTANT: SaveChangesAsync override auto-stamps TenantId and timestamps on new entities.
/// Developers don't set TenantId manually — the context does it.
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ICurrentTenantService _tenantService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    // ─── DbSets ──────────────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<InviteToken> InviteTokens => Set<InviteToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceiptNote> GoodsReceiptNotes => Set<GoodsReceiptNote>();
    public DbSet<GoodsReceiptNoteLine> GoodsReceiptNoteLines => Set<GoodsReceiptNoteLine>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountItem> StockCountItems => Set<StockCountItem>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ─── Configuration ────────────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration classes from this assembly automatically
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ─── GLOBAL QUERY FILTERS ──────────────────────────────────────────────
        // These filters are automatically appended to every LINQ query.
        // Use .IgnoreQueryFilters() in admin-only scenarios where cross-tenant access is needed.
        var tenantId = _tenantService.TenantId;

        modelBuilder.Entity<AppUser>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<InviteToken>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Category>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<StockLevel>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<StockTransfer>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<GoodsReceiptNote>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<StockCount>().HasQueryFilter(e => e.TenantId == tenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.TenantId == tenantId);

        // ─── UNIQUE CONSTRAINTS ────────────────────────────────────────────────
        // SKU codes must be unique per tenant (not globally)
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.SkuCode })
            .IsUnique();

        // Emails must be unique per tenant
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();

        // Only one stock level row per product+warehouse combination
        modelBuilder.Entity<StockLevel>()
            .HasIndex(sl => new { sl.TenantId, sl.ProductId, sl.WarehouseId })
            .IsUnique();
    }

    // ─── Auto-Stamp ──────────────────────────────────────────────────────────
    /// <summary>
    /// Automatically sets TenantId, CreatedAt, and UpdatedAt before saving.
    /// This is the "invisible hand" that ensures every new entity gets the correct tenant.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentTenantId = _tenantService.TenantId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is TenantScopedEntity tenantEntity && entry.State == EntityState.Added)
            {
                // Auto-set TenantId if not already set (prevents accidental cross-tenant data)
                if (tenantEntity.TenantId == Guid.Empty)
                    tenantEntity.TenantId = currentTenantId;
            }

            if (entry.Entity is BaseEntity baseEntity)
            {
                if (entry.State == EntityState.Added)
                    baseEntity.CreatedAt = now;

                if (entry.State is EntityState.Added or EntityState.Modified)
                    baseEntity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
