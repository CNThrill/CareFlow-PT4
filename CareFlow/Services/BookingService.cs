using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CareFlow.Common;
using CareFlow.Data;
using CareFlow.DTOs;
using CareFlow.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

public class BookingService : IBookingService
{
    private readonly CareFlowDbContext _dbContext;

    public BookingService(CareFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// AC-01: View diagnostic services and calculate remaining capacity from persisted data for a selected date.
    /// </summary>
    public async Task<List<ServiceAvailabilityDto>> GetAvailabilityForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;

        // Fetch all diagnostic services
        var services = await _dbContext.DiagnosticServices
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        // Fetch counts of bookings for target date per service
        var bookedCounts = await _dbContext.BookingItems
            .AsNoTracking()
            .Where(bi => bi.Booking.BookingDate.Date == targetDate)
            .GroupBy(bi => bi.DiagnosticServiceId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ServiceId, x => x.Count, cancellationToken);

        var result = new List<ServiceAvailabilityDto>();
        foreach (var s in services)
        {
            bookedCounts.TryGetValue(s.Id, out int booked);
            result.Add(new ServiceAvailabilityDto
            {
                ServiceId = s.Id,
                Code = s.Code,
                Name = s.Name,
                DailyCapacity = s.DailyCapacity,
                BookedCount = booked
            });
        }

        return result;
    }

    /// <summary>
    /// AC-02, AC-03, AC-04, AC-05: Coordinates booking creation under transactional consistency.
    /// Enforces BR-01 through BR-10.
    /// </summary>
    public async Task<BookingResult> CreateBookingAsync(BookingRequest request, CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------------------
        // PRE-TRANSACTION VALIDATIONS
        // -------------------------------------------------------------

        // BR-02: Each booking must contain 1 to 3 distinct diagnostic services
        if (request.SelectedServiceIds == null || request.SelectedServiceIds.Count < 1 || request.SelectedServiceIds.Count > 3)
        {
            return BookingResult.Failure("BR-02 Violation: A booking must contain between 1 and 3 diagnostic services.");
        }

        // BR-06: The same service cannot appear more than once in a booking request
        if (request.SelectedServiceIds.Distinct().Count() != request.SelectedServiceIds.Count)
        {
            return BookingResult.Failure("BR-06 Violation: The same diagnostic service cannot appear more than once in a booking request.");
        }

        // BR-03: The booking date cannot be earlier than current local date
        if (request.BookingDate.Date < DateTime.Today)
        {
            return BookingResult.Failure($"BR-03 Violation: The booking date ({request.BookingDate:yyyy-MM-dd}) cannot be earlier than today ({DateTime.Today:yyyy-MM-dd}).");
        }

        // BR-01: Each booking must reference an existing patient
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PatientId, cancellationToken);

        if (patient == null)
        {
            return BookingResult.Failure($"BR-01 Violation: The selected patient (ID: {request.PatientId}) does not exist in the hospital registry.");
        }

        // -------------------------------------------------------------
        // TRANSACTIONAL UNIT OF WORK (BR-07, BR-08, BR-04, BR-05)
        // -------------------------------------------------------------
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Verify selected services exist in catalog
            var services = await _dbContext.DiagnosticServices
                .Where(s => request.SelectedServiceIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            if (services.Count != request.SelectedServiceIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BookingResult.Failure("One or more selected diagnostic services do not exist in the database.");
            }

            var targetDate = request.BookingDate.Date;

            // BR-08: Authoritative capacity recheck within transaction boundary
            // BR-04 & BR-05: Confirmed bookings cannot exceed daily capacity; each request consumes 1 unit
            foreach (var service in services)
            {
                int alreadyBooked = await _dbContext.BookingItems
                    .CountAsync(bi => bi.DiagnosticServiceId == service.Id && bi.Booking.BookingDate.Date == targetDate, cancellationToken);

                if (alreadyBooked + 1 > service.DailyCapacity)
                {
                    // Rollback immediately - ensure ALL OR NOTHING (BR-07)
                    await transaction.RollbackAsync(cancellationToken);
                    return BookingResult.Failure(
                        $"BR-05/BR-08 Violation: '{service.Name}' ({service.Code}) has reached daily capacity limit ({alreadyBooked}/{service.DailyCapacity}) for {targetDate:yyyy-MM-dd}. Hospital policy rejects the entire booking request.");
                }
            }

            // AC-05: Controlled fault injection for transactional integrity testing
            if (request.SimulateDbFailure)
            {
                throw new InvalidOperationException("Simulated unexpected database failure prior to transaction commit (AC-05 Verification Test).");
            }

            // Create Booking Header
            var phNow = DateTimeHelper.NowPhilippineTime();
            string refNumber = $"CF-{targetDate:yyyyMMdd}-{phNow.Ticks % 10000:D4}";
            var booking = new Booking
            {
                ReferenceNumber = refNumber,
                PatientId = patient.Id,
                BookingDate = targetDate,
                CreatedAt = phNow,
                Status = "Confirmed"
            };

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Create Booking Items (one per selected service)
            foreach (var service in services)
            {
                var item = new BookingItem
                {
                    BookingId = booking.Id,
                    DiagnosticServiceId = service.Id,
                    CreatedAt = phNow
                };
                _dbContext.BookingItems.Add(item);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // BR-07: Atomic commit
            await transaction.CommitAsync(cancellationToken);

            var detailDto = new BookingDetailDto
            {
                Id = booking.Id,
                ReferenceNumber = booking.ReferenceNumber,
                PatientId = patient.Id,
                PatientName = patient.FullName,
                BookingDate = booking.BookingDate,
                CreatedAt = DateTimeHelper.ToPhilippineTime(booking.CreatedAt),
                Status = booking.Status,
                ServiceCodes = services.Select(s => s.Code).ToList(),
                ServiceNames = services.Select(s => s.Name).ToList()
            };

            return BookingResult.Success(detailDto);
        }
        catch (Exception ex)
        {
            // Ensure complete rollback on any database or runtime error
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch
            {
                // Ignore rollback exceptions if transaction was already aborted
            }

            return BookingResult.Failure($"Transaction failed and was safely rolled back. No partial records were saved. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// AC-06 & Feature D: View previously confirmed bookings and associated services.
    /// </summary>
    public async Task<List<BookingDetailDto>> GetAllBookingsAsync(CancellationToken cancellationToken = default)
    {
        var bookings = await _dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Patient)
            .Include(b => b.Items)
                .ThenInclude(i => i.DiagnosticService)
            .OrderByDescending(b => b.BookingDate)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(b => new BookingDetailDto
        {
            Id = b.Id,
            ReferenceNumber = b.ReferenceNumber,
            PatientId = b.PatientId,
            PatientName = b.Patient?.FullName ?? "Unknown",
            BookingDate = b.BookingDate,
            CreatedAt = DateTimeHelper.ToPhilippineTime(b.CreatedAt),
            Status = b.Status,
            ServiceCodes = b.Items.Select(i => i.DiagnosticService.Code).ToList(),
            ServiceNames = b.Items.Select(i => i.DiagnosticService.Name).ToList()
        }).ToList();
    }

    public async Task<BookingDetailDto?> GetBookingByIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        var b = await _dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Patient)
            .Include(b => b.Items)
                .ThenInclude(i => i.DiagnosticService)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (b == null) return null;

        return new BookingDetailDto
        {
            Id = b.Id,
            ReferenceNumber = b.ReferenceNumber,
            PatientId = b.PatientId,
            PatientName = b.Patient?.FullName ?? "Unknown",
            BookingDate = b.BookingDate,
            CreatedAt = DateTimeHelper.ToPhilippineTime(b.CreatedAt),
            Status = b.Status,
            ServiceCodes = b.Items.Select(i => i.DiagnosticService.Code).ToList(),
            ServiceNames = b.Items.Select(i => i.DiagnosticService.Name).ToList()
        };
    }
}
