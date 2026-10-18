using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Models.ViewModels;
using PrawnsFarm.Services;

namespace PrawnsFarm.Controllers;

/// <summary>
/// Manage login credentials. New logins added here are stored in the Users
/// table and can be used immediately to sign in.
/// </summary>
[Authorize]
public class UsersController : Controller
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.OrderBy(u => u.Username).ToListAsync();
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fill in all required fields correctly.";
            return RedirectToAction(nameof(Index));
        }

        var username = model.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            TempData["Error"] = $"A login with username '{username}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Add(new User
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? username : model.DisplayName.Trim(),
            Email = model.Email?.Trim(),
            Phone = model.Phone?.Trim(),
            Role = string.IsNullOrWhiteSpace(model.Role) ? "Member" : model.Role.Trim(),
            PasswordHash = PasswordHasher.Hash(model.Password)
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Login '{username}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        // Never allow deleting the last remaining login.
        if (await _db.Users.CountAsync() <= 1)
        {
            TempData["Error"] = "You cannot delete the only remaining login.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Login '{user.Username}' removed.";
        }
        return RedirectToAction(nameof(Index));
    }
}
