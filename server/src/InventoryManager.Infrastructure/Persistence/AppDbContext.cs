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
public class AppDbContext : DbContext, IAppDbContext
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
        //
        // IMPORTANT: We capture _tenantService (not _tenantService.TenantId) in the lambda.
        // EF Core evaluates the filter expression at query time, not at model-build time.
        // Capturing the value directly would throw for unauthenticated requests (e.g. registration)
        // because TenantId throws when no tenant has been resolved yet.
        modelBuilder.Entity<AppUser>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<InviteToken>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<Category>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<StockLevel>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<StockTransfer>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<GoodsReceiptNote>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<StockCount>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.TenantId == _tenantService.TenantId);

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

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InviteToken>()
            .HasOne(i => i.InvitedBy)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(p => p.Warehouse)
            .WithMany()
            .HasForeignKey(p => p.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockCount>()
           .HasOne(p => p.Warehouse)
           .WithMany()
           .HasForeignKey(p => p.WarehouseId)
           .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
           .HasOne(p => p.FromWarehouse)
           .WithMany()
           .HasForeignKey(p => p.FromWarehouseId)
           .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
           .HasOne(p => p.ToWarehouse)
           .WithMany()
           .HasForeignKey(p => p.ToWarehouseId)
           .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockLevel>()
          .HasOne(p => p.Warehouse)
          .WithMany()
          .HasForeignKey(p => p.WarehouseId)
          .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
          .HasOne(p => p.CreatedByUser)
          .WithMany()
          .HasForeignKey(p => p.CreatedByUserId)
          .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
          .HasOne(p => p.Warehouse)
          .WithMany()
          .HasForeignKey(p => p.WarehouseId)
          .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceiptNote>()
          .HasOne(p => p.Warehouse)
          .WithMany()
          .HasForeignKey(p => p.WarehouseId)
          .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockCountItem>()
            .HasOne(p => p.StockCount)
            .WithMany()
            .HasForeignKey(p => p.StockCountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransferLine>()
            .HasOne(p => p.Transfer)
            .WithMany()
            .HasForeignKey(p => p.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderLine>()
            .HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderLine>()
            .HasOne(p => p.PurchaseOrder)
            .WithMany()
            .HasForeignKey(p => p.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceiptNoteLine>()
            .HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceiptNoteLine>()
            .HasOne(p => p.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(p => p.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // ─── Auto-Stamp ──────────────────────────────────────────────────────────
    /// <summary>
    /// Automatically sets TenantId, CreatedAt, and UpdatedAt before saving.
    /// This is the "invisible hand" that ensures every new entity gets the correct tenant.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is TenantScopedEntity tenantEntity && entry.State == EntityState.Added)
            {
                // Only auto-stamp TenantId for authenticated (tenant-scoped) requests.
                // Registration creates its own Tenant + User with explicit IDs — no tenant in context yet.
                if (tenantEntity.TenantId == Guid.Empty && _tenantService.HasTenant)
                    tenantEntity.TenantId = _tenantService.TenantId;
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
