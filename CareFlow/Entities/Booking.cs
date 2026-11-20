using System;
using System.Collections.Generic;

namespace CareFlow.Entities;

public class Booking
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DateTime BookingDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Confirmed";

    // Navigation property
    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
}
