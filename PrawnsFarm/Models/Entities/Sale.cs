using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrawnsFarm.Models.Entities;

public class Sale
{
    public int Id { get; set; }

    /// <summary>Owning farm (F1 / F2 / F3). Set from the selected farm context.</summary>
    public int FarmId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [MaxLength(120)]
    public string Crop { get; set; } = "Vannamei Prawns";

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 100000000)]
    public decimal QuantityKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 100000000)]
    public decimal RatePerKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 100000000)]
    public decimal Amount { get; set; }

    [MaxLength(120)]
    public string? Buyer { get; set; }

    [MaxLength(20)]
    public string PaymentMode { get; set; } = "Cash";

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
