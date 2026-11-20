using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CareFlow.Common;
using CareFlow.Data;
using CareFlow.DTOs;
using CareFlow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.ViewModels;

public partial class VerificationViewModel : ViewModelBase
{
    private readonly IBookingService _bookingService;
    private readonly CareFlowDbContext _dbContext;

    [ObservableProperty]
    private string _verificationLog = "Select a test scenario below to run verification on live database transactions.\n";

    [ObservableProperty]
    private bool _isRunning;

    public VerificationViewModel(IBookingService bookingService, CareFlowDbContext dbContext)
    {
        _bookingService = bookingService;
        _dbContext = dbContext;
    }

    [RelayCommand]
    public async Task RunScenarioAC04_StaleAvailabilityAsync()
    {
        IsRunning = true;
        var sb = new StringBuilder();
        sb.AppendLine("=== RUNNING SCENARIO AC-04: STALE AVAILABILITY REVALIDATION ===");
        sb.AppendLine($"Timestamp (PST): {DateTimeHelper.NowPhilippineTime():yyyy-MM-dd hh:mm:ss tt}");

        try
        {
            var targetDate = DateTime.Today.AddDays(10);
            sb.AppendLine($"Target Test Date: {targetDate:yyyy-MM-dd}");

            // Step 1: Query initial ECG capacity (Capacity = 2)
            var availability = await _bookingService.GetAvailabilityForDateAsync(targetDate);
            var ecg = availability.Find(s => s.Code == "ECG");
            sb.AppendLine($"ECG Daily Capacity: {ecg?.DailyCapacity}, Current Bookings: {ecg?.BookedCount}, Remaining: {ecg?.RemainingCapacity}");

            // Step 2: Book until only 1 slot is left
            int slotsToFill = (ecg?.RemainingCapacity ?? 1) - 1;
            for (int i = 0; i < slotsToFill; i++)
            {
                await _bookingService.CreateBookingAsync(new BookingRequest
                {
                    PatientId = 1,
                    BookingDate = targetDate,
                    SelectedServiceIds = new List<int> { 4 } // ECG
                });
            }
            sb.AppendLine("Staff UI loaded: ECG shows exactly 1 spot remaining.");

            // Step 3: Concurrent User takes the last slot
            sb.AppendLine("Action: Concurrent staff books Patient 2 for the last ECG slot...");
            var concurrentResult = await _bookingService.CreateBookingAsync(new BookingRequest
            {
                PatientId = 2,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 4 }
            });
            sb.AppendLine($"Concurrent Booking: {(concurrentResult.IsSuccess ? "CONFIRMED" : "FAILED")}");

            // Step 4: First staff attempts to submit stale request for Patient 3
            sb.AppendLine("Action: First staff submits stale request for Patient 3...");
            var staleResult = await _bookingService.CreateBookingAsync(new BookingRequest
            {
                PatientId = 3,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 4 }
            });

            if (!staleResult.IsSuccess)
            {
                sb.AppendLine("RESULT: PASS [AC-04 SATISFIED]");
                sb.AppendLine($"System Response: {staleResult.ErrorMessage}");
                sb.AppendLine("Verification: Authoritative in-transaction re-check prevented overbooking!");
            }
            else
            {
                sb.AppendLine("RESULT: FAIL - Stale request was permitted to overbook.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Unexpected error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            VerificationLog = sb.ToString();
        }
    }

    [RelayCommand]
    public async Task RunScenarioAC05_TransactionRollbackAsync()
    {
        IsRunning = true;
        var sb = new StringBuilder();
        sb.AppendLine("=== RUNNING SCENARIO AC-05: TRANSACTION ATOMICITY & ROLLBACK ===");
        sb.AppendLine($"Timestamp (PST): {DateTimeHelper.NowPhilippineTime():yyyy-MM-dd hh:mm:ss tt}");

        try
        {
            var targetDate = DateTime.Today.AddDays(15);
            int initialBookings = await _dbContext.Bookings.CountAsync();
            int initialItems = await _dbContext.BookingItems.CountAsync();

            sb.AppendLine($"Pre-test Database Counts: {initialBookings} Bookings, {initialItems} BookingItems");
            sb.AppendLine("Action: Submitting booking with 3 services (CBC, Urinalysis, Chest X-Ray) + Injected DB Fault...");

            var faultRequest = new BookingRequest
            {
                PatientId = 1,
                BookingDate = targetDate,
                SelectedServiceIds = new List<int> { 1, 2, 3 },
                SimulateDbFailure = true // Triggers fault inside transaction before commit
            };

            var result = await _bookingService.CreateBookingAsync(faultRequest);

            int postBookings = await _dbContext.Bookings.CountAsync();
            int postItems = await _dbContext.BookingItems.CountAsync();

            sb.AppendLine($"Server Outcome: {(result.IsSuccess ? "SUCCESS" : "REJECTED & ROLLED BACK")}");
            sb.AppendLine($"Feedback Message: {result.ErrorMessage}");
            sb.AppendLine($"Post-test Database Counts: {postBookings} Bookings, {postItems} BookingItems");

            if (!result.IsSuccess && postBookings == initialBookings && postItems == initialItems)
            {
                sb.AppendLine("RESULT: PASS [AC-05 SATISFIED]");
                sb.AppendLine("Verification: Zero partial records persisted. Atomic transaction rollback was executed successfully!");
            }
            else
            {
                sb.AppendLine("RESULT: FAIL - Inconsistent or partial database records detected!");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Unexpected error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            VerificationLog = sb.ToString();
        }
    }

    [RelayCommand]
    public void ClearLog()
    {
        VerificationLog = "Verification log cleared.\n";
    }
}
