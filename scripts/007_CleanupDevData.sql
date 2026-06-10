-- =============================================================================
-- 007_CleanupDevData.sql
-- PURPOSE : Wipe all tenant data and reset the database to a clean state
--           during development. Useful when you want to start fresh without
--           dropping and recreating the whole database.
-- RUN ON  : InventoryManagerDb
-- WARNING : DESTRUCTIVE — deletes ALL data. Development use ONLY.
--           Has a confirmation guard to prevent accidental production runs.
-- ORDER   : Any time during development (optional).
-- =============================================================================

USE InventoryManagerDb;
GO

-- ─── Safety guard ─────────────────────────────────────────────────────────────
-- Uncomment the line below and set to 1 to allow execution.
-- This prevents accidental runs via "Run All" in SSMS.
DECLARE @AllowDelete BIT = 0;   -- Change to 1 to actually run

IF @AllowDelete = 0
BEGIN
    RAISERROR('Safety guard: Set @AllowDelete = 1 to run this script.', 16, 1);
    RETURN;
END
GO

DECLARE @AllowDelete BIT = 1;

IF @AllowDelete = 1
BEGIN
    PRINT 'Cleaning up all tenant data...';

    BEGIN TRANSACTION;
    BEGIN TRY

    -- Delete in dependency order (children before parents to avoid FK violations)
    DELETE FROM GoodsReceiptNoteLines;
    DELETE FROM GoodsReceiptNotes;
    DELETE FROM StockCountItems;
    DELETE FROM StockCounts;
    DELETE FROM StockMovements;
    DELETE FROM StockTransferLines;
    DELETE FROM StockTransfers;
    DELETE FROM PurchaseOrderLines;
    DELETE FROM PurchaseOrders;
    DELETE FROM StockLevels;
    DELETE FROM Notifications;
    DELETE FROM RefreshTokens;
    DELETE FROM InviteTokens;
    DELETE FROM AppUsers;
    DELETE FROM Products;
    DELETE FROM Categories;
    DELETE FROM Suppliers;
    DELETE FROM Warehouses;
    DELETE FROM Tenants;

    COMMIT TRANSACTION;

    PRINT 'All data deleted. Database is clean.';
    PRINT 'Run 004_SeedDemoData.sql to re-seed development data.';

    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        PRINT CONCAT('ERROR during cleanup: ', ERROR_MESSAGE());
        THROW;
    END CATCH;
END
GO
