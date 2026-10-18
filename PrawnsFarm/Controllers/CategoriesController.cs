using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;

namespace PrawnsFarm.Controllers;

// Categories are shared across all farms; this controller is farm-scoped only so
// the shared layout (farm indicator, profile menu) has a selected farm.
public class CategoriesController : FarmScopedController
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var list = await _db.Categories.OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category model)
    {
        if (ModelState.IsValid)
        {
            model.IsActive = true;
            _db.Categories.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category added.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat != null)
        {
            cat.IsActive = !cat.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.Include(c => c.Expenses).FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return RedirectToAction(nameof(Index));

        if (cat.Expenses.Any())
        {
            TempData["Error"] = "Cannot delete a category that has expenses. Deactivate it instead.";
        }
        else
        {
            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}
