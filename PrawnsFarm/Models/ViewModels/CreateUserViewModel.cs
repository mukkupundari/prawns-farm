using System.ComponentModel.DataAnnotations;

namespace PrawnsFarm.Models.ViewModels;

public class CreateUserViewModel
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Display name")]
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [EmailAddress]
    [MaxLength(120)]
    public string? Email { get; set; }

    [Phone]
    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(40)]
    public string? Role { get; set; } = "Member";
}
