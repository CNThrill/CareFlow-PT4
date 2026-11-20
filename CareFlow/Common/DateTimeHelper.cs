using System;

namespace CareFlow.Common;

public static class DateTimeHelper
{
    private static readonly TimeZoneInfo PhilippineTimeZone = ResolvePhilippineTimeZone();

    private static TimeZoneInfo ResolvePhilippineTimeZone()
    {
        // Try Windows standard ID
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
        catch
        {
            // Try IANA ID (Linux / macOS)
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            }
            catch
            {
                // Fallback to UTC+8 fixed offset
                return TimeZoneInfo.CreateCustomTimeZone("PST", TimeSpan.FromHours(8), "Philippine Standard Time", "Philippine Standard Time");
            }
        }
    }

    /// <summary>
    /// Gets current local date and time in Philippine Standard Time (PST, UTC+8).
    /// </summary>
    public static DateTime NowPhilippineTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PhilippineTimeZone);
    }

    /// <summary>
    /// Converts a UTC or unspecified DateTime to Philippine Standard Time (PST, UTC+8).
    /// </summary>
    public static DateTime ToPhilippineTime(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(dateTime.ToUniversalTime(), PhilippineTimeZone);
    }
}
