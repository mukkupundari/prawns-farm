using Microsoft.Data.SqlClient;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Models.ViewModels;

namespace PrawnsFarm.Data;

/// <summary>
/// All filtering / aggregation / reporting reads go through the local SQL
/// Server stored procedures (dbo.usp_*). Writes (add/edit/delete) continue
/// to use EF Core; this repository is read-only.
/// </summary>
public class ReportRepository
{
    private readonly string _connectionString;

    public ReportRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    }

    private SqlConnection Open()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static SqlCommand Proc(SqlConnection conn, string name)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.CommandText = name;
        return cmd;
    }

    // ---------- Dashboard ----------

    public async Task<DashboardSummary> GetSummaryAsync(DateTime from, DateTime to, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Dashboard_Summary");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);

        using var r = await cmd.ExecuteReaderAsync();
        var result = new DashboardSummary();
        if (await r.ReadAsync())
        {
            result.TotalExpenses = r.GetDecimal(r.GetOrdinal("TotalExpenses"));
            result.TotalSales = r.GetDecimal(r.GetOrdinal("TotalSales"));
            result.ExpenseCount = r.GetInt32(r.GetOrdinal("ExpenseCount"));
            result.SaleCount = r.GetInt32(r.GetOrdinal("SaleCount"));
        }
        return result;
    }

    public async Task<List<CategoryBreakdown>> GetExpensesByCategoryAsync(DateTime from, DateTime to, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Expenses_ByCategory");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);

        var list = new List<CategoryBreakdown>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new CategoryBreakdown
            {
                Name = r.GetString(r.GetOrdinal("Name")),
                Color = GetNullableString(r, "Color"),
                Amount = r.GetDecimal(r.GetOrdinal("Amount"))
            });
        }
        return list;
    }

    public async Task<List<CategoryBreakdown>> GetExpensesByPaymentModeAsync(DateTime from, DateTime to, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Expenses_ByPaymentMode");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);

        var list = new List<CategoryBreakdown>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new CategoryBreakdown
            {
                Name = r.GetString(r.GetOrdinal("PaymentMode")),
                Amount = r.GetDecimal(r.GetOrdinal("Amount"))
            });
        }
        return list;
    }

    public async Task<List<Expense>> GetExpensesAsync(DateTime from, DateTime to, int? categoryId, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Expenses_List");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);

        using var r = await cmd.ExecuteReaderAsync();
        return await ReadExpensesAsync(r);
    }

    public async Task<List<Expense>> GetRecentlyAddedExpensesAsync(int farmId, int hours = 24)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Expenses_RecentlyAdded");
        cmd.Parameters.AddWithValue("@FarmId", farmId);
        cmd.Parameters.AddWithValue("@Hours", hours);

        using var r = await cmd.ExecuteReaderAsync();
        return await ReadExpensesAsync(r);
    }

    public async Task<List<Sale>> GetSalesAsync(DateTime from, DateTime to, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Sales_List");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);

        var list = new List<Sale>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Sale
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                Date = r.GetDateTime(r.GetOrdinal("Date")),
                Crop = r.GetString(r.GetOrdinal("Crop")),
                QuantityKg = r.GetDecimal(r.GetOrdinal("QuantityKg")),
                RatePerKg = r.GetDecimal(r.GetOrdinal("RatePerKg")),
                Amount = r.GetDecimal(r.GetOrdinal("Amount")),
                Buyer = GetNullableString(r, "Buyer"),
                PaymentMode = r.GetString(r.GetOrdinal("PaymentMode")),
                Notes = GetNullableString(r, "Notes"),
                CreatedOn = r.GetDateTime(r.GetOrdinal("CreatedOn"))
            });
        }
        return list;
    }

    // ---------- Reports ----------

    public async Task<List<RecentTransaction>> GetTransactionsAsync(DateTime from, DateTime to, string? type, int? categoryId, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Report_Transactions");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);
        cmd.Parameters.AddWithValue("@Type", string.IsNullOrEmpty(type) ? "all" : type);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);

        var list = new List<RecentTransaction>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new RecentTransaction
            {
                Date = r.GetDateTime(r.GetOrdinal("Date")),
                Type = r.GetString(r.GetOrdinal("Type")),
                Category = GetNullableString(r, "Category") ?? string.Empty,
                Description = GetNullableString(r, "Description"),
                Amount = r.GetDecimal(r.GetOrdinal("Amount")),
                PaymentMode = r.GetString(r.GetOrdinal("PaymentMode"))
            });
        }
        return list;
    }

    public async Task<List<MonthlyTrendRow>> GetMonthlyTrendAsync(DateTime from, DateTime to, int farmId)
    {
        using var conn = Open();
        using var cmd = Proc(conn, "dbo.usp_Report_MonthlyTrend");
        cmd.Parameters.AddWithValue("@FromDate", from.Date);
        cmd.Parameters.AddWithValue("@ToDate", to.Date);
        cmd.Parameters.AddWithValue("@FarmId", farmId);

        var list = new List<MonthlyTrendRow>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new MonthlyTrendRow
            {
                Year = r.GetInt32(r.GetOrdinal("Year")),
                Month = r.GetInt32(r.GetOrdinal("Month")),
                Expenses = r.GetDecimal(r.GetOrdinal("Expenses")),
                Sales = r.GetDecimal(r.GetOrdinal("Sales")),
                NetProfit = r.GetDecimal(r.GetOrdinal("NetProfit"))
            });
        }
        return list;
    }

    // ---------- Helpers ----------

    private static async Task<List<Expense>> ReadExpensesAsync(SqlDataReader r)
    {
        var list = new List<Expense>();
        while (await r.ReadAsync())
        {
            list.Add(new Expense
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                Date = r.GetDateTime(r.GetOrdinal("Date")),
                CategoryId = r.GetInt32(r.GetOrdinal("CategoryId")),
                Description = GetNullableString(r, "Description"),
                Amount = r.GetDecimal(r.GetOrdinal("Amount")),
                PaymentMode = r.GetString(r.GetOrdinal("PaymentMode")),
                Notes = GetNullableString(r, "Notes"),
                CreatedOn = r.GetDateTime(r.GetOrdinal("CreatedOn")),
                Category = new Category
                {
                    Id = r.GetInt32(r.GetOrdinal("CategoryId")),
                    Name = r.GetString(r.GetOrdinal("CategoryName")),
                    Color = GetNullableString(r, "CategoryColor")
                }
            });
        }
        return list;
    }

    private static string? GetNullableString(SqlDataReader r, string column)
    {
        int ord = r.GetOrdinal(column);
        return r.IsDBNull(ord) ? null : r.GetString(ord);
    }
}
