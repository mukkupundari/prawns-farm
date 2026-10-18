using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.ViewModels;
using PrawnsFarm.Services;

namespace PrawnsFarm.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        // Never cache the login page: a cached copy (common on mobile / bfcache)
        // carries a stale antiforgery token and causes HTTP 400 on submit.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
        if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new("username", user.Username)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), props);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        // After login the user picks which farm (F1 / F2 / F3) to work in.
        return RedirectToAction("Select", "Farm");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(FarmScopedController.FarmCookie);
        return RedirectToAction("Login");
    }

    // ---------------- Profile (About) ----------------

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await CurrentUserAsync();
        if (user == null) return RedirectToAction(nameof(Login));

        return View(new ProfileViewModel
        {
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            CreatedOn = user.CreatedOn
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await CurrentUserAsync();
        if (user == null) return RedirectToAction(nameof(Login));

        // Username is fixed; the rest is editable.
        model.Username = user.Username;
        model.Role = user.Role;
        model.CreatedOn = user.CreatedOn;

        if (!ModelState.IsValid)
            return View(model);

        user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? user.Username : model.DisplayName.Trim();
        user.Email = model.Email?.Trim();
        user.Phone = model.Phone?.Trim();

        // Optional password change.
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
            user.PasswordHash = PasswordHasher.Hash(model.NewPassword);

        await _db.SaveChangesAsync();

        // Keep the signed-in name in sync with the updated display name.
        var identity = (ClaimsIdentity)User.Identity!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new("username", user.Username)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<Models.Entities.User?> CurrentUserAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(id, out var userId)) return null;
        return await _db.Users.FindAsync(userId);
    }
}
