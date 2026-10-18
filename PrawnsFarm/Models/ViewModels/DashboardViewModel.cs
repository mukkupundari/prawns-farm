using PrawnsFarm.Models.Entities;

namespace PrawnsFarm.Models.ViewModels;

public class DashboardViewModel
{
    public FarmProfile? Farm { get; set; }

    /// <summary>When true the KPI numbers "count up" once on load.</summary>
    public bool Animate { get; set; }

    public DateRangeFilter Filter { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public decimal TotalExpenses { get; set; }
    public decimal TotalSales { get; set; }
    public decimal NetProfit => TotalSales - TotalExpenses;
    public bool IsProfit => NetProfit >= 0;

    public decimal AllTimeExpenses { get; set; }
    public decimal AllTimeSales { get; set; }

    public string? TopCategoryName { get; set; }
    public decimal TopCategoryAmount { get; set; }
    public decimal AveragePerDay { get; set; }

    public int ExpenseCount { get; set; }
    public int SaleCount { get; set; }

    public List<CategoryBreakdown> ExpenseByCategory { get; set; } = new();
    public List<CategoryBreakdown> PaymentModeBreakdown { get; set; } = new();
    public List<MonthlyTrendRow> Trend { get; set; } = new();

    /// <summary>Expenses recorded (CreatedOn) within the last 24 hours.</summary>
    public List<Expense> Last24hExpenses { get; set; } = new();
}

public class CategoryBreakdown
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Color { get; set; }
    public double Percentage { get; set; }
}

public class RecentTransaction
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = "Expense"; // Expense | Sale
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMode { get; set; } = "Cash";
}
