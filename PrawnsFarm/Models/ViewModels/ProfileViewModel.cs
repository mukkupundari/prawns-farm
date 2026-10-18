using System.ComponentModel.DataAnnotations;

namespace PrawnsFarm.Models.ViewModels;

public class ProfileViewModel
{
    public string Username { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Display name")]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(120)]
    public string? Email { get; set; }

    [Phone]
    [MaxLength(30)]
    [Display(Name = "Phone number")]
    public string? Phone { get; set; }

    public string? Role { get; set; }

    public DateTime CreatedOn { get; set; }

    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [Display(Name = "New password (leave blank to keep current)")]
    public string? NewPassword { get; set; }
}
