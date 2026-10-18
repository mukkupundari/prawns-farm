using System.ComponentModel.DataAnnotations;

namespace PrawnsFarm.Models.ViewModels;

public class DateRangeFilter
{
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public int? CategoryId { get; set; }

    /// <summary>Quick-range preset key: today, week, month, year, all.</summary>
    public string? Preset { get; set; }

    public (DateTime from, DateTime to) Resolve()
    {
        var today = DateTime.Today;

        switch (Preset?.ToLowerInvariant())
        {
            case "today":
                return (today, today);
            case "week":
                return (today.AddDays(-6), today);
            case "month":
                return (new DateTime(today.Year, today.Month, 1), today);
            case "year":
                return (new DateTime(today.Year, 1, 1), today);
            case "all":
                return (new DateTime(2000, 1, 1), today);
        }

        var from = From ?? new DateTime(today.Year, today.Month, 1);
        var to = To ?? today;
        if (to < from) (from, to) = (to, from);
        return (from, to);
    }
}
