-- =============================================================================
-- 006_DiagnosticQueries.sql
-- PURPOSE : Developer diagnostic queries for day-to-day debugging.
--           Use these in SSMS during development to inspect live data,
--           verify multi-tenancy isolation, check stock levels, audit trail, etc.
--           SAFE TO RUN ANYTIME — all SELECT statements, nothing is modified.
-- RUN ON  : InventoryManagerDb
-- ORDER   : Any time (read-only).
-- =============================================================================

USE InventoryManagerDb;
GO

-- ─── TIP: Replace @TenantId below with the actual GUID from your demo tenant ─
-- Run: SELECT Id, Name FROM Tenants;   to find it.
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Tenants WHERE Name = 'Demo Company Ltd');

PRINT CONCAT('Querying for TenantId: ', CAST(@TenantId AS NVARCHAR(36)));

-- ──────────────────────────────────────────────────────────────────────────────
-- 1. ALL TENANTS (super-admin view)
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== ALL TENANTS ==';
SELECT
    Id,
    Name,
    Industry,
    CurrencyCode,
    IsActive,
    CreatedAt
FROM Tenants
ORDER BY CreatedAt DESC;

-- ──────────────────────────────────────────────────────────────────────────────
-- 2. USERS FOR A TENANT
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== USERS ==';
SELECT
    u.Id,
    u.FullName,
    u.Email,
    CASE u.Role
        WHEN 1 THEN 'Admin'
        WHEN 2 THEN 'Manager'
        WHEN 3 THEN 'Staff'
        WHEN 4 THEN 'ReadOnly'
    END AS Role,
    u.IsActive,
    u.LastLoginAt,
    u.CreatedAt
FROM AppUsers u
WHERE u.TenantId = @TenantId
ORDER BY u.Role, u.FullName;

-- ──────────────────────────────────────────────────────────────────────────────
-- 3. STOCK LEVELS DASHBOARD (the core view managers use every morning)
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== STOCK LEVELS ==';
SELECT
    w.Name                              AS Warehouse,
    p.SkuCode,
    p.Name                              AS Product,
    c.Name                              AS Category,
    sl.QuantityOnHand,
    sl.QuantityInTransit,
    p.ReorderPoint,
    p.CostPrice,
    ROUND(sl.QuantityOnHand * p.CostPrice, 2) AS StockValue,
    CASE
        WHEN sl.QuantityOnHand <= 0              THEN 'OUT OF STOCK'
        WHEN sl.QuantityOnHand <= p.ReorderPoint THEN 'LOW STOCK'
        ELSE 'OK'
    END AS StockStatus,
    sl.UpdatedAt AS LastUpdated
FROM StockLevels sl
INNER JOIN Products  p ON sl.ProductId   = p.Id
INNER JOIN Warehouses w ON sl.WarehouseId = w.Id
LEFT  JOIN Categories c ON p.CategoryId   = c.Id
WHERE sl.TenantId = @TenantId
  AND p.IsActive  = 1
ORDER BY
    CASE WHEN sl.QuantityOnHand <= p.ReorderPoint THEN 0 ELSE 1 END,  -- Low stock first
    w.Name,
    p.Name;

-- ──────────────────────────────────────────────────────────────────────────────
-- 4. LOW STOCK ITEMS ONLY
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== LOW STOCK ALERTS ==';
SELECT
    w.Name         AS Warehouse,
    p.SkuCode,
    p.Name         AS Product,
    sl.QuantityOnHand AS CurrentQty,
    p.ReorderPoint,
    p.ReorderQuantity AS SuggestedOrderQty
FROM StockLevels sl
INNER JOIN Products   p ON sl.ProductId   = p.Id
INNER JOIN Warehouses w ON sl.WarehouseId = w.Id
WHERE sl.TenantId = @TenantId
  AND p.IsActive  = 1
  AND sl.QuantityOnHand <= p.ReorderPoint
ORDER BY sl.QuantityOnHand ASC;

-- ──────────────────────────────────────────────────────────────────────────────
-- 5. STOCK MOVEMENT AUDIT TRAIL (last 50 movements)
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== RECENT STOCK MOVEMENTS ==';
SELECT TOP 50
    sm.CreatedAt,
    p.Name          AS Product,
    p.SkuCode,
    w.Name          AS Warehouse,
    CASE sm.MovementType
        WHEN 1 THEN 'Opening Balance'
        WHEN 2 THEN 'Goods In (GRN)'
        WHEN 3 THEN 'Manual Adjustment'
        WHEN 4 THEN 'Transfer Out'
        WHEN 5 THEN 'Transfer In'
        WHEN 6 THEN 'Count Adjustment'
        WHEN 7 THEN 'Sale Deduction'
        WHEN 8 THEN 'Write-Off'
    END AS MovementType,
    CASE WHEN sm.QuantityChange > 0 THEN '+' ELSE '' END
        + CAST(sm.QuantityChange AS NVARCHAR(20)) AS QuantityChange,
    sm.QuantityAfter,
    sm.Reason,
    sm.Notes,
    u.FullName AS CreatedBy,
    sm.ReferenceType,
    sm.ReferenceId
FROM StockMovements sm
INNER JOIN Products  p ON sm.ProductId       = p.Id
INNER JOIN Warehouses w ON sm.WarehouseId    = w.Id
INNER JOIN AppUsers  u ON sm.CreatedByUserId = u.Id
WHERE sm.TenantId = @TenantId
ORDER BY sm.CreatedAt DESC;

-- ──────────────────────────────────────────────────────────────────────────────
-- 6. STOCK VALUATION SUMMARY
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== STOCK VALUATION SUMMARY ==';
SELECT
    w.Name                                      AS Warehouse,
    c.Name                                      AS Category,
    COUNT(DISTINCT p.Id)                        AS ProductCount,
    SUM(sl.QuantityOnHand)                      AS TotalUnits,
    ROUND(SUM(sl.QuantityOnHand * p.CostPrice), 2) AS TotalCostValue
FROM StockLevels sl
INNER JOIN Products   p ON sl.ProductId   = p.Id
INNER JOIN Warehouses w ON sl.WarehouseId = w.Id
LEFT  JOIN Categories c ON p.CategoryId   = c.Id
WHERE sl.TenantId = @TenantId
  AND p.IsActive  = 1
GROUP BY w.Name, c.Name
ORDER BY w.Name, TotalCostValue DESC;

-- ──────────────────────────────────────────────────────────────────────────────
-- 7. PURCHASE ORDERS STATUS
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== PURCHASE ORDERS ==';
SELECT
    po.Id,
    s.Name     AS Supplier,
    w.Name     AS DeliveryWarehouse,
    CASE po.Status
        WHEN 1 THEN 'Draft'
        WHEN 2 THEN 'Sent'
        WHEN 3 THEN 'Confirmed'
        WHEN 4 THEN 'Partially Received'
        WHEN 5 THEN 'Fully Received'
        WHEN 6 THEN 'Closed'
        WHEN 7 THEN 'Cancelled'
    END AS Status,
    po.ExpectedDeliveryDate,
    CASE WHEN po.ExpectedDeliveryDate < GETUTCDATE() AND po.Status IN (2,3,4) THEN 'OVERDUE' ELSE '' END AS Alert,
    po.CreatedAt,
    u.FullName AS CreatedBy
FROM PurchaseOrders po
INNER JOIN Suppliers  s ON po.SupplierId      = s.Id
INNER JOIN Warehouses w ON po.WarehouseId     = w.Id
INNER JOIN AppUsers   u ON po.CreatedByUserId = u.Id
WHERE po.TenantId = @TenantId
ORDER BY po.CreatedAt DESC;

-- ──────────────────────────────────────────────────────────────────────────────
-- 8. ACTIVE REFRESH TOKENS (check for suspicious activity)
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== ACTIVE REFRESH TOKENS ==';
SELECT
    u.Email,
    u.FullName,
    rt.CreatedAt  AS IssuedAt,
    rt.ExpiresAt,
    rt.RevokedAt,
    CASE WHEN rt.RevokedAt IS NULL AND rt.ExpiresAt > GETUTCDATE() THEN 'ACTIVE' ELSE 'INACTIVE' END AS Status
FROM RefreshTokens rt
INNER JOIN AppUsers u ON rt.UserId = u.Id
WHERE rt.TenantId = @TenantId
ORDER BY rt.CreatedAt DESC;

-- ──────────────────────────────────────────────────────────────────────────────
-- 9. MULTI-TENANCY ISOLATION CHECK (counts per tenant — should never mix)
-- ──────────────────────────────────────────────────────────────────────────────
PRINT CHAR(13) + '== MULTI-TENANT ISOLATION SUMMARY ==';
SELECT
    t.Name AS TenantName,
    (SELECT COUNT(*) FROM AppUsers    u WHERE u.TenantId = t.Id) AS Users,
    (SELECT COUNT(*) FROM Warehouses  w WHERE w.TenantId = t.Id) AS Warehouses,
    (SELECT COUNT(*) FROM Products    p WHERE p.TenantId = t.Id) AS Products,
    (SELECT COUNT(*) FROM StockLevels sl WHERE sl.TenantId = t.Id) AS StockLevelRows,
    (SELECT COUNT(*) FROM StockMovements sm WHERE sm.TenantId = t.Id) AS StockMovements,
    (SELECT COUNT(*) FROM PurchaseOrders po WHERE po.TenantId = t.Id) AS PurchaseOrders
FROM Tenants t
ORDER BY t.CreatedAt;
GO
