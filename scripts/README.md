# SQL Scripts — InventoryManager SaaS

All SQL scripts for the InventoryManager database. Files are numbered to indicate the correct execution order.

## Execution Order

| # | File | When to Run | Purpose |
|---|------|------------|---------|
| 001 | `001_CreateDatabase.sql` | **Once** — before anything else | Creates the `InventoryManagerDb` database with correct collation and `READ_COMMITTED_SNAPSHOT` |
| 002 | `002_CreateLogin_And_User.sql` | **Once** — after 001 | Creates a least-privilege SQL login `inv_app_user` for the application to connect with |
| 003 | `003_RunEFMigration.sql` | **Once** — after 002 | Guide for running `dotnet-ef database update` + verification queries |
| 004 | `004_SeedDemoData.sql` | **Once per dev reset** | Seeds a demo tenant with warehouses, products, stock levels, and a supplier for testing |
| 005 | `005_VerifySchema.sql` | After migration | Verifies all 20 tables, foreign keys, and unique indexes exist |
| 006 | `006_DiagnosticQueries.sql` | **Any time** (read-only) | Day-to-day debugging: stock levels, low stock alerts, audit trail, PO status, multi-tenant isolation check |
| 007 | `007_CleanupDevData.sql` | On demand | Wipes all data for a clean dev reset. Has a safety guard — set `@AllowDelete = 1` to run |
| 008 | `008_AddMissingIndexes.sql` | After migration | Adds performance indexes for query patterns EF doesn't cover automatically |

## Notes

- **001–003** are one-time setup scripts. Run them in order on a fresh SQL Server.
- **004** seeds enough demo data to immediately test all API endpoints.
- **006** is safe to run at any time — all `SELECT` statements.
- **007** is the "nuke and restart" script. Has a guard flag to prevent accidents.
- **008** should be run after every EF migration in production — idempotent (safe to re-run).

## Connection String Reference

After running 001–002, update `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=InventoryManagerDb;User Id=inv_app_user;Password=StrongP@ssw0rd!;TrustServerCertificate=True;"
}
```

> For local development with Windows Authentication (Trusted Connection), use:
> `"Server=localhost;Database=InventoryManagerDb;Trusted_Connection=True;TrustServerCertificate=True;"`
