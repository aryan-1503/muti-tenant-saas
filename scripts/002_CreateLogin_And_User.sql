-- =============================================================================
-- 002_CreateLogin_And_User.sql
-- PURPOSE : Create a dedicated SQL Server login and database user for the app.
--           The application should NEVER connect as 'sa' or a Windows admin.
-- RUN ON  : SQL Server (master for login, InventoryManagerDb for user)
-- ORDER   : Run AFTER 001_CreateDatabase.sql
-- =============================================================================

-- ─── STEP 1: Create the SQL Server Login (server-level) ──────────────────────
USE master;
GO

IF NOT EXISTS (
    SELECT name FROM sys.server_principals WHERE name = N'inv_app_user'
)
BEGIN
    -- Create a SQL Server authentication login.
    -- Replace 'StrongP@ssw0rd!' with a real password before running in any shared environment.
    CREATE LOGIN inv_app_user
        WITH PASSWORD       = 'StrongP@ssw0rd!',
             DEFAULT_DATABASE = InventoryManagerDb,
             CHECK_POLICY    = ON,   -- Enforce Windows password policy
             CHECK_EXPIRATION = OFF; -- Don't expire app service account passwords

    PRINT 'Login [inv_app_user] created.';
END
ELSE
    PRINT 'Login [inv_app_user] already exists — skipped.';
GO

-- ─── STEP 2: Create the database user and grant permissions ──────────────────
USE InventoryManagerDb;
GO

IF NOT EXISTS (
    SELECT name FROM sys.database_principals WHERE name = N'inv_app_user'
)
BEGIN
    CREATE USER inv_app_user FOR LOGIN inv_app_user;
    PRINT 'Database user [inv_app_user] created.';
END
ELSE
    PRINT 'Database user [inv_app_user] already exists — skipped.';
GO

-- Grant db_datareader + db_datawriter (SELECT, INSERT, UPDATE, DELETE on all tables).
-- Do NOT grant db_owner — principle of least privilege.
ALTER ROLE db_datareader ADD MEMBER inv_app_user;
ALTER ROLE db_datawriter ADD MEMBER inv_app_user;

-- Grant EXECUTE on stored procedures (needed for future SPs if any are added).
GRANT EXECUTE TO inv_app_user;

PRINT 'Permissions granted to [inv_app_user].';
GO

-- ─── STEP 3: Update your connection string ───────────────────────────────────
-- In appsettings.json, change your connection string to:
-- "DefaultConnection": "Server=localhost;Database=InventoryManagerDb;User Id=inv_app_user;Password=StrongP@ssw0rd!;TrustServerCertificate=True;"
