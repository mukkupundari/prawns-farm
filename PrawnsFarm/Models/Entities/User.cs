using System.ComponentModel.DataAnnotations;

namespace PrawnsFarm.Models.Entities;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(120)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(40)]
    public string? Role { get; set; } = "Owner";

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
