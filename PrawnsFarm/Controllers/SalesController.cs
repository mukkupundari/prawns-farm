using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Models.ViewModels;

namespace PrawnsFarm.Controllers;

public class SalesController : FarmScopedController
{
    private readonly AppDbContext _db;
    private readonly ReportRepository _reports;

    public SalesController(AppDbContext db, ReportRepository reports)
    {
        _db = db;
        _reports = reports;
    }

    public async Task<IActionResult> Index(DateRangeFilter filter)
    {
        filter ??= new DateRangeFilter();
        var (from, to) = filter.Resolve();

        // Filtering runs in the DB via dbo.usp_Sales_List.
        var list = await _reports.GetSalesAsync(from, to, CurrentFarmId);

        ViewBag.Filter = filter;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Total = list.Sum(s => s.Amount);
        ViewBag.TotalKg = list.Sum(s => s.QuantityKg);
        return View(list);
    }

    [HttpGet]
    public IActionResult Create() => View(new Sale { Date = DateTime.Today });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Sale model)
    {
        NormalizeAmount(model);
        if (!ModelState.IsValid) return View(model);

        model.FarmId = CurrentFarmId;
        _db.Sales.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Sale recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == id && s.FarmId == CurrentFarmId);
        if (sale == null) return NotFound();
        return View(sale);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Sale model)
    {
        if (id != model.Id) return NotFound();
        NormalizeAmount(model);
        if (!ModelState.IsValid) return View(model);

        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == id && s.FarmId == CurrentFarmId);
        if (sale == null) return NotFound();

        sale.Date = model.Date;
        sale.Crop = model.Crop;
        sale.QuantityKg = model.QuantityKg;
        sale.RatePerKg = model.RatePerKg;
        sale.Amount = model.Amount;
        sale.Buyer = model.Buyer;
        sale.PaymentMode = model.PaymentMode;
        sale.Notes = model.Notes;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Sale updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var sale = await _db.Sales.FirstOrDefaultAsync(s => s.Id == id && s.FarmId == CurrentFarmId);
        if (sale != null)
        {
            _db.Sales.Remove(sale);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Sale deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    // If amount not entered but qty & rate are, compute it.
    private void NormalizeAmount(Sale model)
    {
        if (model.Amount <= 0 && model.QuantityKg > 0 && model.RatePerKg > 0)
        {
            model.Amount = Math.Round(model.QuantityKg * model.RatePerKg, 2);
            ModelState.Remove(nameof(Sale.Amount));
        }
    }
}
