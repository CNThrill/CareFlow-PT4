using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CareFlow.Data;
using CareFlow.DTOs;
using CareFlow.Entities;
using CareFlow.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CareFlow.Tests;

public class CareFlowBusinessRulesAndScenarioTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CareFlowDbContext> _options;

    public CareFlowBusinessRulesAndScenarioTests()
    {
        // Use an open in-memory SQLite connection so schema and data persist across multiple DbContext instances within a test
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<CareFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Initialize database and seed data
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private CareFlowDbContext CreateContext() => new(_options);

    public void Dispose()
    {
        _connection.Dispose();
    }

    // =========================================================================
    // AC-01 & BR-04: View Diagnostic Services & Remaining Capacity
    // =========================================================================
    [Fact]
    public async Task AC01_RemainingCapacity_IsCorrectlyCalculatedFromPersistedBookings()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(1);
        using (var setupContext = CreateContext())
        {
            var booking = new Booking
            {
                ReferenceNumber = "CF-TEST-0001",
                PatientId = 1,
                BookingDate = targetDate,
                CreatedAt = DateTime.UtcNow
            };
            setupContext.Bookings.Add(booking);
            await setupContext.SaveChangesAsync();

            // Book ECG (Id: 4, DailyCapacity: 2)
            setupContext.BookingItems.Add(new BookingItem
            {
                BookingId = booking.Id,
                DiagnosticServiceId = 4
            });
            await setupContext.SaveChangesAsync();
        }

        // Act
        using var queryContext = CreateContext();
        var bookingService = new BookingService(queryContext);
        var availability = await bookingService.GetAvailabilityForDateAsync(targetDate);

        // Assert
        var ecg = availability.First(s => s.Code == "ECG");
        Assert.Equal(2, ecg.DailyCapacity);
        Assert.Equal(1, ecg.BookedCount);
        Assert.Equal(1, ecg.RemainingCapacity);
        Assert.True(ecg.IsAvailable);

        var cbc = availability.First(s => s.Code == "CBC");
        Assert.Equal(5, cbc.DailyCapacity);
        Assert.Equal(0, cbc.BookedCount);
        Assert.Equal(5, cbc.RemainingCapacity);
    }

    // =========================================================================
    // AC-02 & BR-07: Create Multi-Service Booking
    // =========================================================================
    [Fact]
    public async Task AC02_CreateBooking_WithMultipleAvailableServices_SucceedsAtomically()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(2);
        using var context = CreateContext();
        var bookingService = new BookingService(context);

        var request = new BookingRequest
        {
            PatientId = 1,
            BookingDate = targetDate,
            SelectedServiceIds = new List<int> { 1, 2, 3 } // CBC, Urinalysis, Chest X-Ray
        };

        // Act
        var result = await bookingService.CreateBookingAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Booking);
        Assert.Equal(1, result.Booking.PatientId);
        Assert.Equal(3, result.Booking.ServiceCodes.Count);

        // Verify persisted data in a fresh DbContext
        using var verifyContext = CreateContext();
        var persistedBooking = await verifyContext.Bookings
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == result.Booking.Id);

        Assert.NotNull(persistedBooking);
        Assert.Equal(3, persistedBooking.Items.Count);
        Assert.Contains(persistedBooking.Items, i => i.DiagnosticServiceId == 1);
        Assert.Contains(persistedBooking.Items, i => i.DiagnosticServiceId == 2);
        Assert.Contains(persistedBooking.Items, i => i.DiagnosticServiceId == 3);
    }

    // =========================================================================
    // AC-03 & BR-05: Rejection when service reaches daily capacity (No partial records)
    // =========================================================================
    [Fact]
    public async Task AC03_RequestFullyBookedService_RejectsEntireBookingWithoutPartialRecords()
    {
        // Arrange: Fill ECG capacity (Capacity = 2) on target date
        var targetDate = DateTime.Today.AddDays(3);
        using (var setupContext = CreateContext())
        {
            var bookingService = new BookingService(setupContext);
            // First booking: ECG
            await bookingService.CreateBookingAsync(new BookingRequest
            {
                PatientId = 1,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 4 } // ECG
            });
            // Second booking: ECG
            await bookingService.CreateBookingAsync(new BookingRequest
            {
                PatientId = 2,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 4 } // ECG
            });
        }

        // Count bookings before attempt
        using var countContext = CreateContext();
        int initialBookingCount = await countContext.Bookings.CountAsync();
        int initialItemCount = await countContext.BookingItems.CountAsync();

        // Act: Attempt to book CBC (available) AND ECG (fully booked)
        var failedService = new BookingService(countContext);
        var request = new BookingRequest
        {
            PatientId = 3,
            BookingDate = targetDate,
            SelectedServiceIds = new List<int> { 1, 4 } // CBC (avail) + ECG (full)
        };

        var result = await failedService.CreateBookingAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("capacity limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify no partial record was inserted for CBC or booking header
        int finalBookingCount = await countContext.Bookings.CountAsync();
        int finalItemCount = await countContext.BookingItems.CountAsync();

        Assert.Equal(initialBookingCount, finalBookingCount);
        Assert.Equal(initialItemCount, finalItemCount);
    }

    // =========================================================================
    // AC-04 & BR-08: Stale Capacity Revalidation Inside Transaction
    // =========================================================================
    [Fact]
    public async Task AC04_StaleCapacity_RevalidatedInsideTransaction_PreventsOverbooking()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(4);

        // Step 1: Staff reads availability when ECG (capacity: 2) has 1 slot left
        using (var setupContext = CreateContext())
        {
            var bookingService = new BookingService(setupContext);
            await bookingService.CreateBookingAsync(new BookingRequest
            {
                PatientId = 1,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 4 } // ECG booked once (1 spot left)
            });
        }

        // UI queries availability and sees 1 spot left
        using var uiContext = CreateContext();
        var uiService = new BookingService(uiContext);
        var displayedAvailability = await uiService.GetAvailabilityForDateAsync(targetDate);
        var ecgStatus = displayedAvailability.First(s => s.Code == "ECG");
        Assert.Equal(1, ecgStatus.RemainingCapacity); // UI saw 1 slot

        // Meanwhile, another concurrent booking takes the last slot
        using (var concurrentContext = CreateContext())
        {
            var concurrentService = new BookingService(concurrentContext);
            var concurrentResult = await concurrentService.CreateBookingAsync(new BookingRequest
            {
                PatientId = 2,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 4 } // Last spot taken!
            });
            Assert.True(concurrentResult.IsSuccess);
        }

        // Act: First staff tries to submit using the previously viewed information
        var staleSubmissionResult = await uiService.CreateBookingAsync(new BookingRequest
        {
            PatientId = 3,
            BookingDate = targetDate,
            SelectedServiceIds = new List<int> { 4 }
        });

        // Assert: Re-validation caught it and rejected cleanly
        Assert.False(staleSubmissionResult.IsSuccess);
        Assert.Contains("capacity limit", staleSubmissionResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // AC-05 & BR-07: Controlled Database Failure Rolls Back Entire Transaction
    // =========================================================================
    [Fact]
    public async Task AC05_DatabaseFailureBeforeCommit_RollsBackTransactionWithoutPartialPersistence()
    {
        // Arrange
        var targetDate = DateTime.Today.AddDays(5);
        using var context = CreateContext();
        var bookingService = new BookingService(context);

        int initialBookings = await context.Bookings.CountAsync();
        int initialItems = await context.BookingItems.CountAsync();

        var request = new BookingRequest
        {
            PatientId = 1,
            BookingDate = targetDate,
            SelectedServiceIds = new List<int> { 1, 2 },
            SimulateDbFailure = true // Controlled fault injection
        };

        // Act
        var result = await bookingService.CreateBookingAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Transaction failed and was safely rolled back", result.ErrorMessage);

        // Verify zero partial rows were saved
        using var verifyContext = CreateContext();
        int finalBookings = await verifyContext.Bookings.CountAsync();
        int finalItems = await verifyContext.BookingItems.CountAsync();

        Assert.Equal(initialBookings, finalBookings);
        Assert.Equal(initialItems, finalItems);
    }

    // =========================================================================
    // AC-06 & BR-09: Persistence Across Application Restarts
    // =========================================================================
    [Fact]
    public async Task AC06_ConfirmedBookings_PersistAcrossContextInstances()
    {
        // Arrange & Act in Session 1
        var targetDate = DateTime.Today.AddDays(6);
        int createdBookingId;
        using (var session1Context = CreateContext())
        {
            var service1 = new BookingService(session1Context);
            var result = await service1.CreateBookingAsync(new BookingRequest
            {
                PatientId = 2,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 1, 5 } // CBC, Lipid Panel
            });
            Assert.True(result.IsSuccess);
            createdBookingId = result.Booking!.Id;
        }

        // Simulate app restart: open brand new context session
        using (var session2Context = CreateContext())
        {
            var service2 = new BookingService(session2Context);
            var retrieved = await service2.GetBookingByIdAsync(createdBookingId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Maria Clara Santos", retrieved.PatientName);
            Assert.Equal(2, retrieved.ServiceNames.Count);
            Assert.Contains("Complete Blood Count", retrieved.ServiceNames);
            Assert.Contains("Lipid Panel", retrieved.ServiceNames);
        }
    }

    // =========================================================================
    // BR-01: Invalid Patient
    // =========================================================================
    [Fact]
    public async Task BR01_NonExistentPatient_IsRejected()
    {
        using var context = CreateContext();
        var service = new BookingService(context);

        var result = await service.CreateBookingAsync(new BookingRequest
        {
            PatientId = 9999, // Non-existent patient
            BookingDate = DateTime.Today.AddDays(1),
            SelectedServiceIds = new List<int> { 1 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("BR-01", result.ErrorMessage);
    }

    // =========================================================================
    // BR-02: 1 to 3 Distinct Diagnostic Services
    // =========================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task BR02_InvalidServiceCount_IsRejected(int count)
    {
        using var context = CreateContext();
        var service = new BookingService(context);

        var serviceIds = count switch
        {
            0 => new List<int>(),
            4 => new List<int> { 1, 2, 3, 5 },
            _ => new List<int>()
        };

        var result = await service.CreateBookingAsync(new BookingRequest
        {
            PatientId = 1,
            BookingDate = DateTime.Today.AddDays(1),
            SelectedServiceIds = serviceIds
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("BR-02", result.ErrorMessage);
    }

    // =========================================================================
    // BR-03: Past Date Rejection
    // =========================================================================
    [Fact]
    public async Task BR03_PastBookingDate_IsRejected()
    {
        using var context = CreateContext();
        var service = new BookingService(context);

        var result = await service.CreateBookingAsync(new BookingRequest
        {
            PatientId = 1,
            BookingDate = DateTime.Today.AddDays(-1), // Yesterday
            SelectedServiceIds = new List<int> { 1 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("BR-03", result.ErrorMessage);
    }

    // =========================================================================
    // BR-06: Duplicate Service In Request
    // =========================================================================
    [Fact]
    public async Task BR06_DuplicateServicesInRequest_IsRejected()
    {
        using var context = CreateContext();
        var service = new BookingService(context);

        var result = await service.CreateBookingAsync(new BookingRequest
        {
            PatientId = 1,
            BookingDate = DateTime.Today.AddDays(1),
            SelectedServiceIds = new List<int> { 1, 1 } // Duplicate CBC
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("BR-06", result.ErrorMessage);
    }
}
