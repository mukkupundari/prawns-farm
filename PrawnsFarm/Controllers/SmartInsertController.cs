using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PrawnsFarm.Data;
using PrawnsFarm.Models.Entities;
using PrawnsFarm.Models.ViewModels;

namespace PrawnsFarm.Controllers;

/// <summary>
/// Smart Insert: add Expense / Sale records quickly via voice or QR. A record is
/// only created when every required piece of information is present and valid
/// (farm, type, amount, category/source, payment mode). Otherwise the server
/// returns clear prompts telling the user exactly what is still needed.
///
/// This is an additive screen - the normal Expenses/Sales Create flow is
/// untouched.
/// </summary>
public class SmartInsertController : FarmScopedController
{
    private static readonly string[] PaymentModes = { "Cash", "UPI", "Card", "Bank Transfer", "Credit" };

    private readonly AppDbContext _db;

    public SmartInsertController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await PopulateLookups();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SmartInsertDto dto)
    {
        var result = new SmartInsertResult();

        if (dto == null)
        {
            result.Errors.Add("Nothing was captured. Please try again.");
            return Json(result);
        }

        var type = (dto.Type ?? "").Trim().ToLowerInvariant();
        if (type != "expense" && type != "sale")
        {
            result.Errors.Add("Tell me whether this is an EXPENSE or a SALE.");
        }

        // Which farm is always explicit from the selected-farm context.
        var farm = CurrentFarm!;
        result.Normalized["Farm"] = $"{farm.Code} · {farm.FarmName}";

        var date = (dto.Date?.Date) ?? DateTime.Today;
        result.Normalized["Date"] = date.ToString("dd MMM yyyy");

        var mode = NormalizePaymentMode(dto.PaymentMode);

        if (type == "expense")
        {
            // Amount
            if (dto.Amount is null || dto.Amount <= 0)
                result.Errors.Add("Say the EXPENSE amount (e.g. \"five thousand rupees\").");
            else
                result.Normalized["Amount"] = "₹ " + dto.Amount.Value.ToString("N2");

            // Category (the "source" of the expense)
            var category = await ResolveExpenseCategoryAsync(dto.CategoryId, dto.Category);
            if (category == null)
                result.Errors.Add("Say a valid CATEGORY for the expense (e.g. Feed, Labor, Power).");
            else
                result.Normalized["Category"] = category.Name;

            // Payment mode / source
            if (mode == null)
                result.Errors.Add("Say the PAYMENT MODE / source (Cash, UPI, Card, Bank Transfer or Credit).");
            else
                result.Normalized["Payment Mode"] = mode;

            if (result.Errors.Count > 0) return Json(result);

            var expense = new Expense
            {
                FarmId = CurrentFarmId,
                Date = date,
                CategoryId = category!.Id,
                Amount = decimal.Round(dto.Amount!.Value, 2),
                PaymentMode = mode!,
                Description = Trim(dto.Description, 250),
                Notes = Trim(BuildNotes(dto), 500)
            };
            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();

            result.Ok = true;
            result.Id = expense.Id;
            result.Message = $"Expense of ₹{expense.Amount:N2} ({category.Name}) added to {farm.FarmName}.";
            return Json(result);
        }

        if (type == "sale")
        {
            // Amount can be given directly OR derived from qty x rate.
            decimal? amount = dto.Amount;
            if ((amount is null || amount <= 0) && dto.QuantityKg is > 0 && dto.RatePerKg is > 0)
                amount = decimal.Round(dto.QuantityKg.Value * dto.RatePerKg.Value, 2);

            if (amount is null || amount <= 0)
                result.Errors.Add("Say the SALE amount, or the quantity in kg and the rate per kg.");
            else
                result.Normalized["Amount"] = "₹ " + amount.Value.ToString("N2");

            if (mode == null)
                result.Errors.Add("Say the PAYMENT MODE / source (Cash, UPI, Card, Bank Transfer or Credit).");
            else
                result.Normalized["Payment Mode"] = mode;

            if (dto.QuantityKg is > 0) result.Normalized["Quantity"] = $"{dto.QuantityKg} kg";
            if (dto.RatePerKg is > 0) result.Normalized["Rate"] = $"₹ {dto.RatePerKg}/kg";

            if (result.Errors.Count > 0) return Json(result);

            var crop = string.IsNullOrWhiteSpace(dto.Crop) ? "Vannamei Prawns" : Trim(dto.Crop, 120)!;
            result.Normalized["Crop"] = crop;

            var sale = new Sale
            {
                FarmId = CurrentFarmId,
                Date = date,
                Crop = crop,
                QuantityKg = dto.QuantityKg is > 0 ? dto.QuantityKg!.Value : 0,
                RatePerKg = dto.RatePerKg is > 0 ? dto.RatePerKg!.Value : 0,
                Amount = decimal.Round(amount!.Value, 2),
                Buyer = Trim(dto.Buyer, 120),
                PaymentMode = mode!,
                Notes = Trim(BuildNotes(dto), 500)
            };
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            result.Ok = true;
            result.Id = sale.Id;
            result.Message = $"Sale of ₹{sale.Amount:N2} recorded for {farm.FarmName}.";
            return Json(result);
        }

        // type invalid: errors already set above
        return Json(result);
    }

    // ---------- helpers ----------

    private async Task PopulateLookups()
    {
        var cats = await _db.Categories
            .Where(c => c.IsActive && c.Type == CategoryType.Expense)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        ViewBag.Categories = cats;
        ViewBag.CategorySelect = new SelectList(cats, "Id", "Name");
        ViewBag.PaymentModes = PaymentModes;
    }

    private async Task<Category?> ResolveExpenseCategoryAsync(int? id, string? name)
    {
        if (id is > 0)
        {
            var byId = await _db.Categories.FirstOrDefaultAsync(c =>
                c.Id == id && c.IsActive && c.Type == CategoryType.Expense);
            if (byId != null) return byId;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim().ToLower();
            var cats = await _db.Categories
                .Where(c => c.IsActive && c.Type == CategoryType.Expense)
                .ToListAsync();

            // exact, then contains, then starts-with match (case-insensitive)
            return cats.FirstOrDefault(c => c.Name.ToLower() == n)
                ?? cats.FirstOrDefault(c => c.Name.ToLower().Contains(n) || n.Contains(c.Name.ToLower()));
        }

        return null;
    }

    private static string? NormalizePaymentMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        var m = mode.Trim().ToLowerInvariant();
        return m switch
        {
            "cash" => "Cash",
            "upi" or "gpay" or "google pay" or "phonepe" or "paytm" => "UPI",
            "card" or "credit card" or "debit card" => "Card",
            "bank" or "bank transfer" or "neft" or "rtgs" or "imps" or "transfer" => "Bank Transfer",
            "credit" or "due" or "udhaar" or "udhar" => "Credit",
            _ => PaymentModes.FirstOrDefault(p => p.Equals(mode.Trim(), StringComparison.OrdinalIgnoreCase))
        };
    }

    private static string? BuildNotes(SmartInsertDto dto)
    {
        var note = string.IsNullOrWhiteSpace(dto.Notes) ? "" : dto.Notes.Trim();
        var src = string.IsNullOrWhiteSpace(dto.Transcript) ? "" : $"[Smart Insert] \"{dto.Transcript.Trim()}\"";
        return string.Join(" ", new[] { note, src }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
