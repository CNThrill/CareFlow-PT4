using System;
using System.Collections.Generic;

namespace CareFlow.DTOs;

public class PatientDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string ContactNumber { get; set; } = string.Empty;

    public string DisplayText => $"{FullName} (DOB: {DateOfBirth:yyyy-MM-dd})";
}

public class DiagnosticServiceDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DailyCapacity { get; set; }
}

public class ServiceAvailabilityDto
{
    public int ServiceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DailyCapacity { get; set; }
    public int BookedCount { get; set; }
    public int RemainingCapacity => Math.Max(0, DailyCapacity - BookedCount);
    public bool IsAvailable => RemainingCapacity > 0;
    public string StatusText => RemainingCapacity switch
    {
        <= 0 => "FULL",
        1 => "1 spot left",
        _ => $"{RemainingCapacity} available"
    };
}

public class BookingRequest
{
    public int PatientId { get; set; }
    public DateTime BookingDate { get; set; }
    public List<int> SelectedServiceIds { get; set; } = new();

    /// <summary>
    /// For verifying AC-05 (transaction rollback): simulates an internal database fault before transaction commit.
    /// </summary>
    public bool SimulateDbFailure { get; set; }
}

public class BookingDetailDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedAtPstFormatted => $"{CreatedAt:yyyy-MM-dd hh:mm:ss tt} PST";
    public string Status { get; set; } = "Confirmed";
    public List<string> ServiceNames { get; set; } = new();
    public List<string> ServiceCodes { get; set; } = new();

    public string ServicesSummary => string.Join(", ", ServiceNames);
}

public class BookingResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public BookingDetailDto? Booking { get; }

    private BookingResult(bool isSuccess, string? errorMessage, BookingDetailDto? booking)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Booking = booking;
    }

    public static BookingResult Success(BookingDetailDto booking) => new(true, null, booking);
    public static BookingResult Failure(string errorMessage) => new(false, errorMessage, null);
}
