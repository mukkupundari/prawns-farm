using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;

namespace PrawnsFarm.Controllers;

/// <summary>
/// Base controller for every screen that works "inside" a farm. It resolves the
/// currently selected farm from a cookie, makes it available as
/// <see cref="CurrentFarmId"/> / <see cref="CurrentFarm"/> and exposes it to the
/// layout via ViewData. If no (valid) farm is selected the user is sent to the
/// farm picker first.
/// </summary>
[Authorize]
public abstract class FarmScopedController : Controller
{
    public const string FarmCookie = "pf_farm";

    protected int CurrentFarmId { get; private set; }
    protected FarmProfile? CurrentFarm { get; private set; }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        FarmProfile? farm = null;
        if (int.TryParse(Request.Cookies[FarmCookie], out var id) && id > 0)
            farm = await db.FarmProfiles.FindAsync(id);

        if (farm == null || !farm.IsActive)
        {
            context.Result = RedirectToAction("Select", "Farm");
            return;
        }

        CurrentFarmId = farm.Id;
        CurrentFarm = farm;
        ViewData["CurrentFarm"] = farm;

        await next();
    }
}
