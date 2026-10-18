using Microsoft.AspNetCore.Mvc;
using PrawnsFarm.Data;
using PrawnsFarm.Models.ViewModels;

namespace PrawnsFarm.Controllers;

public class DashboardController : FarmScopedController
{
    private readonly ReportRepository _reports;

    public DashboardController(ReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index(DateRangeFilter filter)
    {
        filter ??= new DateRangeFilter();
        var (from, to) = filter.Resolve();
        var days = Math.Max(1, (to.Date - from.Date).Days + 1);

        // All aggregates come from stored procedures (local DB), scoped to the farm.
        var summary = await _reports.GetSummaryAsync(from, to, CurrentFarmId);
        var allTime = await _reports.GetSummaryAsync(new DateTime(2000, 1, 1), DateTime.Today, CurrentFarmId);

        var vm = new DashboardViewModel
        {
            Farm = CurrentFarm,
            Filter = filter,
            // Play the running-numbers animation once right after entering a farm.
            Animate = TempData["Animate"] as bool? ?? false,
            FromDate = from,
            ToDate = to,
            TotalExpenses = summary.TotalExpenses,
            TotalSales = summary.TotalSales,
            ExpenseCount = summary.ExpenseCount,
            SaleCount = summary.SaleCount,
            AllTimeExpenses = allTime.TotalExpenses,
            AllTimeSales = allTime.TotalSales,
            AveragePerDay = Math.Round(summary.TotalExpenses / days, 0)
        };

        vm.ExpenseByCategory = await _reports.GetExpensesByCategoryAsync(from, to, CurrentFarmId);
        foreach (var c in vm.ExpenseByCategory)
            c.Percentage = summary.TotalExpenses == 0 ? 0 : (double)(c.Amount / summary.TotalExpenses * 100);

        var top = vm.ExpenseByCategory.FirstOrDefault();
        vm.TopCategoryName = top?.Name;
        vm.TopCategoryAmount = top?.Amount ?? 0;

        vm.PaymentModeBreakdown = await _reports.GetExpensesByPaymentModeAsync(from, to, CurrentFarmId);
        foreach (var p in vm.PaymentModeBreakdown)
            p.Percentage = summary.TotalExpenses == 0 ? 0 : (double)(p.Amount / summary.TotalExpenses * 100);

        vm.Trend = await _reports.GetMonthlyTrendAsync(from, to, CurrentFarmId);
        vm.Last24hExpenses = await _reports.GetRecentlyAddedExpensesAsync(CurrentFarmId, 24);

        return View(vm);
    }
}
