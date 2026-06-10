-- =============================================================================
-- 008_AddMissingIndexes.sql
-- PURPOSE : Add performance indexes that EF Core doesn't generate automatically.
--           EF creates PK and unique indexes — this script adds FK indexes and
--           query-pattern indexes that will matter at scale (1000+ rows).
-- RUN ON  : InventoryManagerDb (after EF migration)
-- ORDER   : After 003. Safe to run multiple times (checks before creating).
-- =============================================================================

USE InventoryManagerDb;
GO

-- ──────────────────────────────────────────────────────────────────────────────
-- STOCK MOVEMENTS
-- The most-queried table. Needs indexes for every common filter pattern.
-- ──────────────────────────────────────────────────────────────────────────────

-- Filter by tenant + date range (the most common query pattern — audit trail)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_TenantId_CreatedAt')
    CREATE NONCLUSTERED INDEX IX_StockMovements_TenantId_CreatedAt
    ON StockMovements (TenantId, CreatedAt DESC)
    INCLUDE (ProductId, WarehouseId, MovementType, QuantityChange, QuantityAfter, CreatedByUserId);

-- Filter by product (product movement history page)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_TenantId_ProductId')
    CREATE NONCLUSTERED INDEX IX_StockMovements_TenantId_ProductId
    ON StockMovements (TenantId, ProductId, CreatedAt DESC);

-- Filter by warehouse (location-level audit trail)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_TenantId_WarehouseId')
    CREATE NONCLUSTERED INDEX IX_StockMovements_TenantId_WarehouseId
    ON StockMovements (TenantId, WarehouseId, CreatedAt DESC);

-- Filter by movement type (e.g. show only adjustments, only GoodsIn)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_TenantId_MovementType')
    CREATE NONCLUSTERED INDEX IX_StockMovements_TenantId_MovementType
    ON StockMovements (TenantId, MovementType, CreatedAt DESC);

-- ──────────────────────────────────────────────────────────────────────────────
-- STOCK LEVELS
-- ──────────────────────────────────────────────────────────────────────────────

-- Filter by warehouse (warehouse stock view)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockLevels_TenantId_WarehouseId')
    CREATE NONCLUSTERED INDEX IX_StockLevels_TenantId_WarehouseId
    ON StockLevels (TenantId, WarehouseId)
    INCLUDE (ProductId, QuantityOnHand, QuantityInTransit, UpdatedAt);

-- ──────────────────────────────────────────────────────────────────────────────
-- PRODUCTS
-- ──────────────────────────────────────────────────────────────────────────────

-- Filter by category (category-level views, stock counts by category)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_TenantId_CategoryId')
    CREATE NONCLUSTERED INDEX IX_Products_TenantId_CategoryId
    ON Products (TenantId, CategoryId)
    WHERE CategoryId IS NOT NULL;

-- Name search (product search / autocomplete)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_TenantId_Name')
    CREATE NONCLUSTERED INDEX IX_Products_TenantId_Name
    ON Products (TenantId, Name)
    INCLUDE (SkuCode, CostPrice, SellPrice, ReorderPoint, IsActive);

-- ──────────────────────────────────────────────────────────────────────────────
-- PURCHASE ORDERS
-- ──────────────────────────────────────────────────────────────────────────────

-- Filter by status (open POs dashboard)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrders_TenantId_Status')
    CREATE NONCLUSTERED INDEX IX_PurchaseOrders_TenantId_Status
    ON PurchaseOrders (TenantId, Status, ExpectedDeliveryDate)
    INCLUDE (SupplierId, WarehouseId, CreatedAt);

-- Filter by supplier (supplier order history)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrders_TenantId_SupplierId')
    CREATE NONCLUSTERED INDEX IX_PurchaseOrders_TenantId_SupplierId
    ON PurchaseOrders (TenantId, SupplierId, CreatedAt DESC);

-- ──────────────────────────────────────────────────────────────────────────────
-- STOCK TRANSFERS
-- ──────────────────────────────────────────────────────────────────────────────

-- Filter by status (pending transfers dashboard)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockTransfers_TenantId_Status')
    CREATE NONCLUSTERED INDEX IX_StockTransfers_TenantId_Status
    ON StockTransfers (TenantId, Status, CreatedAt DESC);

-- ──────────────────────────────────────────────────────────────────────────────
-- NOTIFICATIONS
-- ──────────────────────────────────────────────────────────────────────────────

-- Unread notifications per user (notification bell count)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_TenantId_UserId_IsRead')
    CREATE NONCLUSTERED INDEX IX_Notifications_TenantId_UserId_IsRead
    ON Notifications (TenantId, UserId, IsRead, CreatedAt DESC);

-- ──────────────────────────────────────────────────────────────────────────────
-- REFRESH TOKENS
-- ──────────────────────────────────────────────────────────────────────────────

-- Look up by token value (used on every refresh call)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Token')
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_Token
    ON RefreshTokens (Token)
    INCLUDE (UserId, TenantId, ExpiresAt, RevokedAt);

-- ──────────────────────────────────────────────────────────────────────────────
-- STOCK COUNTS
-- ──────────────────────────────────────────────────────────────────────────────

-- Filter by warehouse + status (active counts per location)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockCounts_TenantId_WarehouseId_Status')
    CREATE NONCLUSTERED INDEX IX_StockCounts_TenantId_WarehouseId_Status
    ON StockCounts (TenantId, WarehouseId, Status);

PRINT 'All performance indexes created (or already existed).';
GO

-- ─── Verify all indexes ───────────────────────────────────────────────────────
SELECT
    t.name  AS TableName,
    i.name  AS IndexName,
    i.type_desc AS IndexType,
    i.is_unique
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name LIKE 'IX_%'
ORDER BY t.name, i.name;
GO
