using System.Collections.Generic;

namespace CareFlow.Entities;

public class DiagnosticService
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DailyCapacity { get; set; }

    // Navigation property
    public ICollection<BookingItem> BookingItems { get; set; } = new List<BookingItem>();
}
