using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrawnsFarm.Models.Entities;

public class FarmProfile
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string FarmName { get; set; } = string.Empty;

    /// <summary>Short code shown as the farm indicator, e.g. F1 / F2 / F3.</summary>
    [MaxLength(10)]
    public string? Code { get; set; }

    [MaxLength(120)]
    public string? Location { get; set; }

    [MaxLength(80)]
    public string? FarmType { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal LandAreaAcres { get; set; }

    [MaxLength(20)]
    public string? Established { get; set; }

    /// <summary>Accent colour used for the farm card / indicator badge.</summary>
    [MaxLength(20)]
    public string? AccentColor { get; set; }

    public bool IsActive { get; set; } = true;
}
