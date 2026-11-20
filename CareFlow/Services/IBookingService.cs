using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CareFlow.DTOs;

namespace CareFlow.Services;

public interface IBookingService
{
    Task<List<ServiceAvailabilityDto>> GetAvailabilityForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<BookingResult> CreateBookingAsync(BookingRequest request, CancellationToken cancellationToken = default);
    Task<List<BookingDetailDto>> GetAllBookingsAsync(CancellationToken cancellationToken = default);
    Task<BookingDetailDto?> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default);
}
