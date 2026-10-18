using System.ComponentModel.DataAnnotations;

namespace PrawnsFarm.Models.Entities;

public enum CategoryType
{
    Expense = 0,
    Income = 1
}

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public CategoryType Type { get; set; } = CategoryType.Expense;

    [MaxLength(30)]
    public string? Icon { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
