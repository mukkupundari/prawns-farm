namespace PrawnsFarm.Models.ViewModels;

/// <summary>
/// Payload produced by the Smart Insert screen (voice / QR) and posted to the
/// server for strict validation before a record is created.
/// </summary>
public class SmartInsertDto
{
    /// <summary>"expense" or "sale".</summary>
    public string? Type { get; set; }

    public decimal? Amount { get; set; }

    // Expense
    public int? CategoryId { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }

    // Sale
    public string? Crop { get; set; }
    public decimal? QuantityKg { get; set; }
    public decimal? RatePerKg { get; set; }
    public string? Buyer { get; set; }

    // Common
    public string? PaymentMode { get; set; }
    public DateTime? Date { get; set; }
    public string? Notes { get; set; }

    /// <summary>Raw voice transcript / QR payload, stored for traceability.</summary>
    public string? Transcript { get; set; }
}

/// <summary>Result returned to the Smart Insert UI.</summary>
public class SmartInsertResult
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
    public int? Id { get; set; }

    /// <summary>Human-readable prompts for anything missing/invalid.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Normalised values echoed back for the confirmation summary.</summary>
    public Dictionary<string, string> Normalized { get; set; } = new();
}
