-- =============================================================================
-- 005_VerifySchema.sql
-- PURPOSE : Verify the database schema is correct after EF migration.
--           Run this in SSMS to confirm all tables, columns, indexes, and
--           foreign keys exist as expected.
-- RUN ON  : InventoryManagerDb (after EF migration)
-- ORDER   : After 003.
-- =============================================================================

USE InventoryManagerDb;
GO

PRINT '========== TABLE COUNT ==========';
SELECT
    COUNT(*) AS TotalTables
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';
-- Expected: 20 tables + 1 (__EFMigrationsHistory) = 21 rows

PRINT '';
PRINT '========== ALL TABLES ==========';
SELECT
    TABLE_NAME,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c
     WHERE c.TABLE_NAME = t.TABLE_NAME) AS ColumnCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

-- Expected tables:
-- AppUsers, Categories, GoodsReceiptNoteLines, GoodsReceiptNotes,
-- InviteTokens, Notifications, Products, PurchaseOrderLines, PurchaseOrders,
-- RefreshTokens, StockCountItems, StockCounts, StockLevels, StockMovements,
-- StockTransferLines, StockTransfers, Suppliers, Tenants, Warehouses,
-- __EFMigrationsHistory

PRINT '';
PRINT '========== FOREIGN KEYS ==========';
SELECT
    fk.name                     AS ForeignKey,
    tp.name                     AS ParentTable,
    cp.name                     AS ParentColumn,
    tr.name                     AS ReferencedTable,
    cr.name                     AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables  tp ON fkc.parent_object_id   = tp.object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id   = cp.object_id AND fkc.parent_column_id   = cp.column_id
INNER JOIN sys.tables  tr ON fkc.referenced_object_id = tr.object_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
ORDER BY tp.name, fk.name;

PRINT '';
PRINT '========== UNIQUE INDEXES (Tenant-scoped) ==========';
SELECT
    t.name   AS TableName,
    i.name   AS IndexName,
    i.is_unique,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.is_unique = 1
  AND i.is_primary_key = 0
ORDER BY t.name, i.name;

-- Expected unique indexes:
-- AppUsers: IX_AppUsers_TenantId_Email
-- Products: IX_Products_TenantId_SkuCode
-- StockLevels: IX_StockLevels_TenantId_ProductId_WarehouseId

PRINT '';
PRINT '========== MIGRATIONS APPLIED ==========';
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;
GO
