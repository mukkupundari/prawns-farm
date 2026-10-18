using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Models.ViewModels;

namespace PrawnsFarm.Controllers;

public class ReportsController : FarmScopedController
{
    private readonly AppDbContext _db;
    private readonly ReportRepository _reports;

    public ReportsController(AppDbContext db, ReportRepository reports)
    {
        _db = db;
        _reports = reports;
    }

    public async Task<IActionResult> Index(DateRangeFilter filter, string? type)
    {
        filter ??= new DateRangeFilter();
        var (from, to) = filter.Resolve();
        var rows = await _reports.GetTransactionsAsync(from, to, type, filter.CategoryId, CurrentFarmId);

        ViewBag.Filter = filter;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Type = type ?? "all";
        ViewBag.TotalExpenses = rows.Where(r => r.Type == "Expense").Sum(r => r.Amount);
        ViewBag.TotalSales = rows.Where(r => r.Type == "Sale").Sum(r => r.Amount);
        await PopulateCategories(filter.CategoryId);

        return View(rows);
    }

    // CSV download (data comes from dbo.usp_Report_Transactions)
    public async Task<IActionResult> Export(DateRangeFilter filter, string? type)
    {
        filter ??= new DateRangeFilter();
        var (from, to) = filter.Resolve();
        var rows = await _reports.GetTransactionsAsync(from, to, type, filter.CategoryId, CurrentFarmId);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Type,Category,Description,Amount,PaymentMode");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.Date.ToString("yyyy-MM-dd"),
                Csv(r.Type),
                Csv(r.Category),
                Csv(r.Description ?? ""),
                r.Amount.ToString(CultureInfo.InvariantCulture),
                Csv(r.PaymentMode)));
        }

        var totalExp = rows.Where(r => r.Type == "Expense").Sum(r => r.Amount);
        var totalSale = rows.Where(r => r.Type == "Sale").Sum(r => r.Amount);
        sb.AppendLine();
        sb.AppendLine($"Total Expenses,{totalExp.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Total Sales,{totalSale.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Net Profit/Loss,{(totalSale - totalExp).ToString(CultureInfo.InvariantCulture)}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"PrawnsFarm_Report_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    // Excel (.xlsx) download with a data sheet + summary sheet.
    public async Task<IActionResult> ExportExcel(DateRangeFilter filter, string? type)
    {
        filter ??= new DateRangeFilter();
        var (from, to) = filter.Resolve();

        var rows = await _reports.GetTransactionsAsync(from, to, type, filter.CategoryId, CurrentFarmId);
        var byCategory = await _reports.GetExpensesByCategoryAsync(from, to, CurrentFarmId);
        var summary = await _reports.GetSummaryAsync(from, to, CurrentFarmId);

        using var wb = new XLWorkbook();

        // ---- Transactions sheet ----
        var ws = wb.Worksheets.Add("Transactions");
        ws.Cell(1, 1).Value = $"{CurrentFarm?.FarmName ?? "Prawns Farm"} - Transactions Report";
        var titleRange = ws.Range(1, 1, 1, 6);
        titleRange.Merge();
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = $"Period: {from:dd MMM yyyy} to {to:dd MMM yyyy}";
        var subRange = ws.Range(2, 1, 2, 6);
        subRange.Merge();
        subRange.Style.Font.Italic = true;

        var header = 4;
        string[] cols = { "Date", "Type", "Category / Crop", "Description", "Amount (₹)", "Payment Mode" };
        for (int i = 0; i < cols.Length; i++)
        {
            var cell = ws.Cell(header, i + 1);
            cell.Value = cols[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#16a34a");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = header + 1;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Date;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd-MMM-yyyy";
            ws.Cell(row, 2).Value = r.Type;
            ws.Cell(row, 3).Value = r.Category;
            ws.Cell(row, 4).Value = r.Description ?? "";
            ws.Cell(row, 5).Value = r.Amount;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = r.PaymentMode;
            row++;
        }

        // Totals
        var totalExp = rows.Where(r => r.Type == "Expense").Sum(r => r.Amount);
        var totalSale = rows.Where(r => r.Type == "Sale").Sum(r => r.Amount);
        row++;
        ws.Cell(row, 4).Value = "Total Expenses";
        ws.Cell(row, 5).Value = totalExp;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 4).Value = "Total Sales";
        ws.Cell(row, 5).Value = totalSale;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 4).Value = "Net Profit / Loss";
        ws.Cell(row, 5).Value = totalSale - totalExp;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();

        // ---- Summary sheet (expense by category) ----
        var ws2 = wb.Worksheets.Add("Expense by Category");
        ws2.Cell(1, 1).Value = "Category";
        ws2.Cell(1, 2).Value = "Amount (₹)";
        ws2.Range(1, 1, 1, 2).Style.Font.Bold = true;
        ws2.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#16a34a");
        ws2.Range(1, 1, 1, 2).Style.Font.FontColor = XLColor.White;
        int r2 = 2;
        foreach (var c in byCategory)
        {
            ws2.Cell(r2, 1).Value = c.Name;
            ws2.Cell(r2, 2).Value = c.Amount;
            ws2.Cell(r2, 2).Style.NumberFormat.Format = "#,##0.00";
            r2++;
        }
        ws2.Cell(r2, 1).Value = "Total";
        ws2.Cell(r2, 2).Value = summary.TotalExpenses;
        ws2.Range(r2, 1, r2, 2).Style.Font.Bold = true;
        ws2.Cell(r2, 2).Style.NumberFormat.Format = "#,##0.00";
        ws2.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"PrawnsFarm_Report_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private async Task PopulateCategories(int? selected)
    {
        var cats = await _db.Categories.Where(c => c.Type == CategoryType.Expense)
            .OrderBy(c => c.Name).ToListAsync();
        ViewBag.Categories = new SelectList(cats, "Id", "Name", selected);
    }
}
