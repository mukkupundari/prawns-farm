using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;

namespace PrawnsFarm.Controllers;

/// <summary>
/// Farm picker shown right after login. Choosing a farm stores its id in a
/// cookie; every farm-scoped screen then reads that cookie.
/// </summary>
[Authorize]
public class FarmController : Controller
{
    private readonly AppDbContext _db;

    public FarmController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Select()
    {
        var farms = await _db.FarmProfiles
            .Where(f => f.IsActive)
            .OrderBy(f => f.Id)
            .ToListAsync();

        // Highlight the currently active farm (if any).
        if (int.TryParse(Request.Cookies[FarmScopedController.FarmCookie], out var current))
            ViewBag.CurrentFarmId = current;

        return View(farms);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(int id)
    {
        var farm = await _db.FarmProfiles.FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (farm == null)
        {
            TempData["Error"] = "That farm is not available.";
            return RedirectToAction(nameof(Select));
        }

        Response.Cookies.Append(FarmScopedController.FarmCookie, farm.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        // Trigger the dashboard count-up animation once on entering a farm.
        TempData["Animate"] = true;
        TempData["Success"] = $"Now viewing {farm.FarmName}.";
        return RedirectToAction("Index", "Dashboard");
    }

    // Convenience link in the profile menu to go back to the picker.
    [HttpGet]
    public IActionResult Switch()
    {
        Response.Cookies.Delete(FarmScopedController.FarmCookie);
        return RedirectToAction(nameof(Select));
    }
}
