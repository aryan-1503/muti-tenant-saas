-- =============================================================================
-- 003_RunEFMigration.sql
-- PURPOSE : Instructions for running the EF Core migration from the CLI.
--           This is NOT a T-SQL script to run in SSMS — it documents the CLI
--           commands you run in a terminal to apply the EF schema to the DB.
-- ORDER   : Run AFTER 001 and 002.
-- =============================================================================

/*
──────────────────────────────────────────────────────────────────────────────
  HOW TO APPLY THE EF CORE MIGRATION
──────────────────────────────────────────────────────────────────────────────

  PRE-REQUISITES:
  1. SQL Server is running locally.
  2. Database 'InventoryManagerDb' exists (run 001_CreateDatabase.sql).
  3. Login 'inv_app_user' exists (run 002_CreateLogin_And_User.sql).
  4. appsettings.json has the correct connection string.
  5. dotnet-ef tool is installed: dotnet tool install --global dotnet-ef

  APPLY MIGRATION (run from the repo root):
  ─────────────────────────────────────────
  dotnet-ef database update `
    --project server/src/InventoryManager.Infrastructure/InventoryManager.Infrastructure.csproj `
    --startup-project server/src/InventoryManager.API/InventoryManager.API.csproj

  This will:
  - Run the InitialCreate migration
  - Create all 20 tables in InventoryManagerDb
  - Create the __EFMigrationsHistory table (tracks applied migrations)

  VERIFY (in SSMS or Azure Data Studio):
  ──────────────────────────────────────
  USE InventoryManagerDb;
  SELECT * FROM __EFMigrationsHistory;

  You should see one row: InitialCreate

  ADD A NEW MIGRATION (when you change domain entities):
  ───────────────────────────────────────────────────────
  dotnet-ef migrations add <MigrationName> `
    --project server/src/InventoryManager.Infrastructure/InventoryManager.Infrastructure.csproj `
    --startup-project server/src/InventoryManager.API/InventoryManager.API.csproj `
    --output-dir Persistence/Migrations

  ROLLBACK A MIGRATION:
  ─────────────────────
  -- Roll back to a specific migration (pass migration name, not timestamp):
  dotnet-ef database update <PreviousMigrationName> `
    --project server/src/InventoryManager.Infrastructure/InventoryManager.Infrastructure.csproj `
    --startup-project server/src/InventoryManager.API/InventoryManager.API.csproj

  -- Remove the last unapplied migration file:
  dotnet-ef migrations remove `
    --project server/src/InventoryManager.Infrastructure/InventoryManager.Infrastructure.csproj `
    --startup-project server/src/InventoryManager.API/InventoryManager.API.csproj

──────────────────────────────────────────────────────────────────────────────
*/

-- ─── Verify the migration was applied ────────────────────────────────────────
USE InventoryManagerDb;
GO

SELECT
    MigrationId,
    ProductVersion,
    GETUTCDATE() AS CheckedAt
FROM __EFMigrationsHistory
ORDER BY MigrationId;
GO

-- ─── Show all tables created by EF ───────────────────────────────────────────
SELECT
    TABLE_SCHEMA,
    TABLE_NAME,
    TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;
GO
