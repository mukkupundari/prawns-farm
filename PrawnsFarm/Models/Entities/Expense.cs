using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrawnsFarm.Models.Entities;

public class Expense
{
    public int Id { get; set; }

    /// <summary>Owning farm (F1 / F2 / F3). Set from the selected farm context.</summary>
    public int FarmId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [MaxLength(250)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 100000000)]
    public decimal Amount { get; set; }

    [MaxLength(20)]
    public string PaymentMode { get; set; } = "Cash";

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
