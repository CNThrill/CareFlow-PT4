using System;

namespace CareFlow.Entities;

public class BookingItem
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int DiagnosticServiceId { get; set; }
    public DiagnosticService DiagnosticService { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
