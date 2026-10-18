/* =====================================================================
   Prawns Farm - Schema Reference
   ---------------------------------------------------------------------
   NOTE: The tables below are created automatically by Entity Framework
   Core (EnsureCreated) the first time the app runs. This script is a
   REFERENCE ONLY so you can see the exact table/column names when
   writing stored procedures. Do NOT run it if the DB already exists.
   ===================================================================== */

-- Users (login)
--   Id            INT IDENTITY PK
--   Username      NVARCHAR(50)  UNIQUE
--   PasswordHash  NVARCHAR(MAX)   (PBKDF2: iterations.salt.hash)
--   DisplayName   NVARCHAR(100)
--   Email         NVARCHAR(120)  (nullable)
--   Phone         NVARCHAR(30)   (nullable)
--   Role          NVARCHAR(40)   (nullable)
--   CreatedOn     DATETIME2

-- Categories
--   Id        INT IDENTITY PK
--   Name      NVARCHAR(60)
--   Type      INT            (0 = Expense, 1 = Income)
--   Icon      NVARCHAR(30)
--   Color     NVARCHAR(20)
--   IsActive  BIT

-- Expenses
--   Id           INT IDENTITY PK
--   FarmId       INT            (indexed; F1/F2/F3 scope)
--   Date         DATETIME2      (indexed)
--   CategoryId   INT  FK -> Categories.Id
--   Description  NVARCHAR(250)
--   Amount       DECIMAL(18,2)
--   PaymentMode  NVARCHAR(20)
--   Notes        NVARCHAR(500)
--   CreatedOn    DATETIME2

-- Sales
--   Id           INT IDENTITY PK
--   FarmId       INT            (indexed; F1/F2/F3 scope)
--   Date         DATETIME2      (indexed)
--   Crop         NVARCHAR(120)
--   QuantityKg   DECIMAL(18,2)
--   RatePerKg    DECIMAL(18,2)
--   Amount       DECIMAL(18,2)
--   Buyer        NVARCHAR(120)
--   PaymentMode  NVARCHAR(20)
--   Notes        NVARCHAR(500)
--   CreatedOn    DATETIME2

-- FarmProfiles  (one row per farm: F1 / F2 / F3)
--   Id             INT IDENTITY PK
--   FarmName       NVARCHAR(120)
--   Code           NVARCHAR(10)   (e.g. F1/F2/F3)
--   Location       NVARCHAR(120)
--   FarmType       NVARCHAR(80)
--   LandAreaAcres  DECIMAL(10,2)
--   Established    NVARCHAR(20)
--   AccentColor    NVARCHAR(20)
--   IsActive       BIT
