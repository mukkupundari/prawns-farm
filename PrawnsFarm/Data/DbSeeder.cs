using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Services;

namespace PrawnsFarm.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // Bring pre-existing databases up to date with new columns (idempotent).
        await SchemaUpgrader.UpgradeAsync(db);

        // Default login
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                DisplayName = "Prawn Farmer",
                Email = "owner@prawnsfarm.local",
                Phone = "",
                Role = "Owner",
                PasswordHash = PasswordHasher.Hash("admin123")
            });
        }

        // Farm profiles - we run three farms (F1 / F2 / F3).
        var farmDefaults = new[]
        {
            new FarmProfile { FarmName = "Sri Lakshmi Prawns Farm", Code = "F1", Location = "West Godavari, AP", FarmType = "Vannamei Prawns", LandAreaAcres = 20, Established = "2024", AccentColor = "#16a34a", IsActive = true },
            new FarmProfile { FarmName = "Krishna Delta Farm",      Code = "F2", Location = "Krishna, AP",       FarmType = "Vannamei Prawns", LandAreaAcres = 15, Established = "2024", AccentColor = "#3b82f6", IsActive = true },
            new FarmProfile { FarmName = "Godavari Aqua Farm",       Code = "F3", Location = "East Godavari, AP",  FarmType = "Tiger Prawns",    LandAreaAcres = 25, Established = "2025", AccentColor = "#a855f7", IsActive = true }
        };

        var existingFarms = await db.FarmProfiles.OrderBy(f => f.Id).ToListAsync();

        // Backfill metadata on farms that already exist (pre-multi-farm DBs).
        for (int i = 0; i < existingFarms.Count && i < farmDefaults.Length; i++)
        {
            var f = existingFarms[i];
            if (string.IsNullOrEmpty(f.Code)) f.Code = farmDefaults[i].Code;
            if (string.IsNullOrEmpty(f.AccentColor)) f.AccentColor = farmDefaults[i].AccentColor;
        }

        // Create any missing farms so we always have three to choose from.
        for (int i = existingFarms.Count; i < farmDefaults.Length; i++)
            db.FarmProfiles.Add(farmDefaults[i]);

        // Default categories
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Feed", Type = CategoryType.Expense, Icon = "bi-basket", Color = "#22c55e" },
                new Category { Name = "Labor", Type = CategoryType.Expense, Icon = "bi-people", Color = "#3b82f6" },
                new Category { Name = "Power", Type = CategoryType.Expense, Icon = "bi-lightning-charge", Color = "#f59e0b" },
                new Category { Name = "Medicine", Type = CategoryType.Expense, Icon = "bi-capsule", Color = "#a855f7" },
                new Category { Name = "Fuel", Type = CategoryType.Expense, Icon = "bi-fuel-pump", Color = "#ef4444" },
                new Category { Name = "Seed / Larvae", Type = CategoryType.Expense, Icon = "bi-egg", Color = "#06b6d4" },
                new Category { Name = "Maintenance", Type = CategoryType.Expense, Icon = "bi-tools", Color = "#14b8a6" },
                new Category { Name = "Others", Type = CategoryType.Expense, Icon = "bi-three-dots", Color = "#64748b" },
                new Category { Name = "Crop Sale", Type = CategoryType.Income, Icon = "bi-cash-coin", Color = "#16a34a" }
            );
        }

        await db.SaveChangesAsync();
    }
}
