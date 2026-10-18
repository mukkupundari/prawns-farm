using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Models.ViewModels;

namespace PrawnsFarm.Controllers;

public class ExpensesController : FarmScopedController
{
    private readonly AppDbContext _db;
    private readonly ReportRepository _reports;

    public ExpensesController(AppDbContext db, ReportRepository reports)
    {
        _db = db;
        _reports = reports;
    }

    public async Task<IActionResult> Index(DateRangeFilter filter)
    {
        filter ??= new DateRangeFilter();
        var (from, to) = filter.Resolve();

        // Filtering runs in the DB via dbo.usp_Expenses_List.
        var list = await _reports.GetExpensesAsync(from, to, filter.CategoryId, CurrentFarmId);

        ViewBag.Filter = filter;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Total = list.Sum(e => e.Amount);
        await PopulateCategories(filter.CategoryId);
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateCategories(null);
        return View(new Expense { Date = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Expense model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategories(model.CategoryId);
            return View(model);
        }

        model.FarmId = CurrentFarmId;
        _db.Expenses.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Expense added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.FarmId == CurrentFarmId);
        if (expense == null) return NotFound();
        await PopulateCategories(expense.CategoryId);
        return View(expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Expense model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateCategories(model.CategoryId);
            return View(model);
        }

        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.FarmId == CurrentFarmId);
        if (expense == null) return NotFound();

        expense.Date = model.Date;
        expense.CategoryId = model.CategoryId;
        expense.Description = model.Description;
        expense.Amount = model.Amount;
        expense.PaymentMode = model.PaymentMode;
        expense.Notes = model.Notes;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Expense updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.FarmId == CurrentFarmId);
        if (expense != null)
        {
            _db.Expenses.Remove(expense);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Expense deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCategories(int? selected)
    {
        var cats = await _db.Categories
            .Where(c => c.IsActive && c.Type == CategoryType.Expense)
            .OrderBy(c => c.Name)
            .ToListAsync();
        ViewBag.Categories = new SelectList(cats, "Id", "Name", selected);
    }
}
