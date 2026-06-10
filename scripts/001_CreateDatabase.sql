-- =============================================================================
-- 001_CreateDatabase.sql
-- PURPOSE : Create the InventoryManager database and configure it.
-- RUN ON  : SQL Server instance (master context)
-- ORDER   : Run FIRST before any other script.
-- =============================================================================

USE master;
GO

-- ─── Safety check — only create if it doesn't already exist ──────────────────
IF NOT EXISTS (
    SELECT name FROM sys.databases WHERE name = N'InventoryManagerDb'
)
BEGIN
    CREATE DATABASE InventoryManagerDb
    COLLATE SQL_Latin1_General_CP1_CI_AS;   -- Case-insensitive, accent-sensitive (standard for SMB apps)

    PRINT 'Database [InventoryManagerDb] created successfully.';
END
ELSE
BEGIN
    PRINT 'Database [InventoryManagerDb] already exists — skipped creation.';
END
GO

-- ─── Switch to the new database ──────────────────────────────────────────────
USE InventoryManagerDb;
GO

-- ─── Set recommended options ─────────────────────────────────────────────────
ALTER DATABASE InventoryManagerDb SET READ_COMMITTED_SNAPSHOT ON;
-- READ_COMMITTED_SNAPSHOT allows readers not to block writers and vice versa.
-- This is the standard setting for EF Core apps to avoid deadlocks under concurrent load.

ALTER DATABASE InventoryManagerDb SET RECOVERY SIMPLE;
-- SIMPLE recovery for development. Change to FULL for production with proper backup plan.

PRINT 'Database options configured.';
GO
