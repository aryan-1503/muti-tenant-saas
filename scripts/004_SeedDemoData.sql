-- =============================================================================
-- 004_SeedDemoData.sql
-- PURPOSE : Insert a demo tenant, admin user, and sample data for development
--           and manual testing. Makes it easy to start the API and immediately
--           have something to query/test with.
-- RUN ON  : InventoryManagerDb (AFTER the EF migration has been applied)
-- ORDER   : Run AFTER 003 (migration applied).
-- WARNING : For DEVELOPMENT ONLY. Never run on production.
-- =============================================================================

USE InventoryManagerDb;
GO

-- ─── Guard: Skip if demo tenant already exists ───────────────────────────────
IF EXISTS (SELECT 1 FROM Tenants WHERE Name = 'Demo Company Ltd')
BEGIN
    PRINT 'Demo data already seeded — skipping.';
    RETURN;
END
GO

BEGIN TRANSACTION;
BEGIN TRY

-- ─── 1. Tenant ───────────────────────────────────────────────────────────────
DECLARE @TenantId   UNIQUEIDENTIFIER = NEWID();
DECLARE @Now        DATETIME2        = GETUTCDATE();

INSERT INTO Tenants (Id, Name, Industry, CurrencyCode, TimezoneId, LogoUrl, IsActive, CreatedAt)
VALUES (
    @TenantId,
    'Demo Company Ltd',
    'Retail',
    'USD',
    'Asia/Kolkata',
    NULL,
    1,
    @Now
);

PRINT CONCAT('Tenant created: ', CAST(@TenantId AS NVARCHAR(36)));

-- ─── 2. Admin User ───────────────────────────────────────────────────────────
-- Password hash below = BCrypt hash of "Admin@1234"
-- Generate a real hash at runtime — this is only for seeding dev data.
DECLARE @AdminUserId UNIQUEIDENTIFIER = NEWID();

INSERT INTO AppUsers (Id, TenantId, Email, PasswordHash, FullName, Role, IsActive, CreatedAt)
VALUES (
    @AdminUserId,
    @TenantId,
    'admin@democompany.com',
    '$2a$11$demoHashPlaceholder.ForDevSeedingOnly.ReplaceWithRealBCrypt',
    'Admin User',
    1,   -- UserRole.Admin = 1
    1,
    @Now
);

PRINT CONCAT('Admin user created: ', CAST(@AdminUserId AS NVARCHAR(36)));

-- ─── 3. Manager User ─────────────────────────────────────────────────────────
DECLARE @ManagerUserId UNIQUEIDENTIFIER = NEWID();

INSERT INTO AppUsers (Id, TenantId, Email, PasswordHash, FullName, Role, IsActive, CreatedAt)
VALUES (
    @ManagerUserId,
    @TenantId,
    'manager@democompany.com',
    '$2a$11$demoHashPlaceholder.ForDevSeedingOnly.ReplaceWithRealBCrypt',
    'Warehouse Manager',
    2,   -- UserRole.Manager = 2
    1,
    @Now
);

-- ─── 4. Warehouses ───────────────────────────────────────────────────────────
DECLARE @Warehouse1Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Warehouse2Id UNIQUEIDENTIFIER = NEWID();

INSERT INTO Warehouses (Id, TenantId, Name, Address, Type, IsActive, CreatedAt)
VALUES
    (@Warehouse1Id, @TenantId, 'Main Warehouse',    '123 Industrial Road, Mumbai', 'Warehouse', 1, @Now),
    (@Warehouse2Id, @TenantId, 'Retail Store Front', '45 MG Road, Pune',           'Retail Store', 1, @Now);

-- ─── 5. Categories ───────────────────────────────────────────────────────────
DECLARE @CatElectronics UNIQUEIDENTIFIER = NEWID();
DECLARE @CatBeverages   UNIQUEIDENTIFIER = NEWID();
DECLARE @CatCleaning    UNIQUEIDENTIFIER = NEWID();

INSERT INTO Categories (Id, TenantId, Name, Description, CreatedAt)
VALUES
    (@CatElectronics, @TenantId, 'Electronics',      'Phones, cables, accessories',     @Now),
    (@CatBeverages,   @TenantId, 'Beverages',         'Cold drinks, water, juices',      @Now),
    (@CatCleaning,    @TenantId, 'Cleaning Supplies', 'Detergents, mops, sprays',        @Now);

-- ─── 6. Products ─────────────────────────────────────────────────────────────
DECLARE @Prod1 UNIQUEIDENTIFIER = NEWID();   -- Phone Charger
DECLARE @Prod2 UNIQUEIDENTIFIER = NEWID();   -- USB-C Cable
DECLARE @Prod3 UNIQUEIDENTIFIER = NEWID();   -- Mineral Water 1L
DECLARE @Prod4 UNIQUEIDENTIFIER = NEWID();   -- Cola 330ml
DECLARE @Prod5 UNIQUEIDENTIFIER = NEWID();   -- Floor Cleaner 5L

INSERT INTO Products (Id, TenantId, CategoryId, Name, SkuCode, Description, Barcode,
                      UnitOfMeasure, CostPrice, SellPrice, ReorderPoint, ReorderQuantity,
                      MinStockLevel, IsActive, CreatedAt)
VALUES
    (@Prod1, @TenantId, @CatElectronics, 'Phone Charger 20W',   'ELEC-CHG-20W',   '20W Fast Charger',           '8901234567890', 'Each',  350.00,  599.00,  20, 100,  10, 1, @Now),
    (@Prod2, @TenantId, @CatElectronics, 'USB-C Cable 1m',      'ELEC-USBC-1M',   '1-meter braided USB-C cable','8901234567891', 'Each',  80.00,   149.00,  30, 150,  10, 1, @Now),
    (@Prod3, @TenantId, @CatBeverages,   'Mineral Water 1L',    'BEV-WATER-1L',   'Packaged drinking water',    '8901234512345', 'Each',  12.00,   20.00,   50, 500,  25, 1, @Now),
    (@Prod4, @TenantId, @CatBeverages,   'Cola 330ml Can',      'BEV-COLA-330',   'Carbonated cola drink',      '8901234512346', 'Each',  22.00,   40.00,   48, 288,  24, 1, @Now),
    (@Prod5, @TenantId, @CatCleaning,    'Floor Cleaner 5L',    'CLEAN-FLOOR-5L', 'Heavy duty floor cleaner',   NULL,            'Litre', 180.00,  299.00,  10,  50,   5, 1, @Now);

-- ─── 7. Stock Levels (Opening Balances) ──────────────────────────────────────
-- Warehouse 1 (Main Warehouse)
INSERT INTO StockLevels (Id, TenantId, ProductId, WarehouseId, QuantityOnHand, QuantityInTransit, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @TenantId, @Prod1, @Warehouse1Id, 145, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod2, @Warehouse1Id, 280, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod3, @Warehouse1Id, 600, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod4, @Warehouse1Id, 240, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod5, @Warehouse1Id,  18, 0, @Now, @Now);  -- Below reorder point!

-- Warehouse 2 (Retail Store Front)
INSERT INTO StockLevels (Id, TenantId, ProductId, WarehouseId, QuantityOnHand, QuantityInTransit, CreatedAt, UpdatedAt)
VALUES
    (NEWID(), @TenantId, @Prod1, @Warehouse2Id,  32, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod2, @Warehouse2Id,  45, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod3, @Warehouse2Id,  80, 0, @Now, @Now),
    (NEWID(), @TenantId, @Prod4, @Warehouse2Id,  15, 0, @Now, @Now),  -- Below reorder point!
    (NEWID(), @TenantId, @Prod5, @Warehouse2Id,   4, 0, @Now, @Now);  -- Below reorder point!

-- ─── 8. Opening Balance Stock Movements (audit trail entries) ────────────────
INSERT INTO StockMovements (Id, TenantId, ProductId, WarehouseId, MovementType, QuantityChange,
                             QuantityAfter, ReferenceType, ReferenceId, Reason, Notes,
                             CreatedByUserId, CreatedAt)
VALUES
    -- Warehouse 1
    (NEWID(), @TenantId, @Prod1, @Warehouse1Id, 1, 145, 145, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod2, @Warehouse1Id, 1, 280, 280, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod3, @Warehouse1Id, 1, 600, 600, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod4, @Warehouse1Id, 1, 240, 240, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod5, @Warehouse1Id, 1,  18,  18, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    -- Warehouse 2
    (NEWID(), @TenantId, @Prod1, @Warehouse2Id, 1,  32,  32, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod2, @Warehouse2Id, 1,  45,  45, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod3, @Warehouse2Id, 1,  80,  80, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod4, @Warehouse2Id, 1,  15,  15, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now),
    (NEWID(), @TenantId, @Prod5, @Warehouse2Id, 1,   4,   4, 'OpeningBalance', NULL, 'Initial stock entry', 'Seed data', @AdminUserId, @Now);

-- ─── 9. Supplier ─────────────────────────────────────────────────────────────
DECLARE @Supplier1 UNIQUEIDENTIFIER = NEWID();

INSERT INTO Suppliers (Id, TenantId, Name, ContactName, Email, Phone, Address,
                       LeadTimeDays, PaymentTerms, Notes, IsActive, CreatedAt)
VALUES (
    @Supplier1,
    @TenantId,
    'Metro Electronics Pvt Ltd',
    'Rajan Mehta',
    'rajan@metroelectronics.com',
    '+91-98765-43210',
    '7th Floor, Trade Tower, Bandra Kurla Complex, Mumbai',
    5,
    'Net 30',
    'Primary electronics supplier. Reliable delivery track record.',
    1,
    @Now
);

PRINT 'Demo data seeded successfully.';
PRINT CONCAT('TenantId: ', CAST(@TenantId AS NVARCHAR(36)));
PRINT 'Use this TenantId in API calls during development.';

COMMIT TRANSACTION;

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT CONCAT('ERROR: ', ERROR_MESSAGE());
    THROW;
END CATCH;
GO
