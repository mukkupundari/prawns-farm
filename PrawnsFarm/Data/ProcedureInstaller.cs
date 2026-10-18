using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace PrawnsFarm.Data;

/// <summary>
/// Creates / updates the reporting stored procedures in the local database
/// by executing Sql/02_StoredProcedures.sql on startup. The script uses
/// CREATE OR ALTER, so running it repeatedly is safe (idempotent).
/// </summary>
public static class ProcedureInstaller
{
    public static async Task ApplyAsync(AppDbContext db, IWebHostEnvironment env, ILogger logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Sql", "02_StoredProcedures.sql");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Sql", "02_StoredProcedures.sql");

        if (!File.Exists(path))
        {
            logger.LogWarning("Stored procedure script not found. Skipping SP installation. Looked in {Path}", path);
            return;
        }

        var script = await File.ReadAllTextAsync(path);

        // Split into batches on lines containing only GO.
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        int applied = 0;
        foreach (var raw in batches)
        {
            // Remove USE statements - the app connection is already scoped to the DB.
            var batch = Regex.Replace(raw, @"^\s*USE\s+.+?$", string.Empty,
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (!ContainsExecutableSql(batch))
                continue;

            await db.Database.ExecuteSqlRawAsync(batch);
            applied++;
        }

        logger.LogInformation("Installed/updated {Count} stored procedure batches.", applied);
    }

    // True if the batch has SQL once comments/whitespace are stripped.
    private static bool ContainsExecutableSql(string batch)
    {
        var noBlock = Regex.Replace(batch, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var noLine = Regex.Replace(noBlock, @"--.*?$", string.Empty, RegexOptions.Multiline);
        return !string.IsNullOrWhiteSpace(noLine);
    }
}
