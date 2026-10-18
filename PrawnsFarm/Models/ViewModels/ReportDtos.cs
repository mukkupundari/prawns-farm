namespace PrawnsFarm.Models.ViewModels;

public class DashboardSummary
{
    public decimal TotalExpenses { get; set; }
    public decimal TotalSales { get; set; }
    public int ExpenseCount { get; set; }
    public int SaleCount { get; set; }
    public decimal NetProfit => TotalSales - TotalExpenses;
}

public class MonthlyTrendRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Expenses { get; set; }
    public decimal Sales { get; set; }
    public decimal NetProfit { get; set; }
}
