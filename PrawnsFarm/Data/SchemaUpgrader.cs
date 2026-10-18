using Microsoft.EntityFrameworkCore;

namespace PrawnsFarm.Data;

/// <summary>
/// The app uses <c>EnsureCreated()</c> (not EF migrations), so when new columns
/// are added to existing entities an already-created database will NOT pick them
/// up automatically. This upgrader applies small, idempotent <c>ALTER TABLE</c>
/// statements so that both fresh and pre-existing databases end up with the
/// multi-farm / profile columns. It is safe to run on every startup.
/// </summary>
public static class SchemaUpgrader
{
    public static async Task UpgradeAsync(AppDbContext db)
    {
        const string sql = @"
-- Multi-farm scoping
IF COL_LENGTH('dbo.Expenses','FarmId') IS NULL
    ALTER TABLE dbo.Expenses ADD FarmId INT NOT NULL CONSTRAINT DF_Expenses_FarmId DEFAULT(1);
IF COL_LENGTH('dbo.Sales','FarmId') IS NULL
    ALTER TABLE dbo.Sales ADD FarmId INT NOT NULL CONSTRAINT DF_Sales_FarmId DEFAULT(1);

-- Farm profile metadata
IF COL_LENGTH('dbo.FarmProfiles','Code') IS NULL
    ALTER TABLE dbo.FarmProfiles ADD Code NVARCHAR(10) NULL;
IF COL_LENGTH('dbo.FarmProfiles','AccentColor') IS NULL
    ALTER TABLE dbo.FarmProfiles ADD AccentColor NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.FarmProfiles','IsActive') IS NULL
    ALTER TABLE dbo.FarmProfiles ADD IsActive BIT NOT NULL CONSTRAINT DF_FarmProfiles_IsActive DEFAULT(1);

-- User profile details / logins
IF COL_LENGTH('dbo.Users','Email') IS NULL
    ALTER TABLE dbo.Users ADD Email NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.Users','Phone') IS NULL
    ALTER TABLE dbo.Users ADD Phone NVARCHAR(30) NULL;
IF COL_LENGTH('dbo.Users','Role') IS NULL
    ALTER TABLE dbo.Users ADD Role NVARCHAR(40) NULL;

-- Helpful indexes (ignore if they already exist)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_FarmId' AND object_id = OBJECT_ID('dbo.Expenses'))
    CREATE INDEX IX_Expenses_FarmId ON dbo.Expenses(FarmId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_FarmId' AND object_id = OBJECT_ID('dbo.Sales'))
    CREATE INDEX IX_Sales_FarmId ON dbo.Sales(FarmId);
";

        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
