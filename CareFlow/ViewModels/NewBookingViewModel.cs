using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CareFlow.DTOs;
using CareFlow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CareFlow.ViewModels;

public partial class NewBookingViewModel : ViewModelBase
{
    private readonly IBookingService _bookingService;
    private readonly IPatientService _patientService;

    public event Action? BookingConfirmed;

    public ObservableCollection<PatientDto> Patients { get; } = new();
    public ObservableCollection<SelectableServiceItemViewModel> Services { get; } = new();

    [ObservableProperty]
    private PatientDto? _selectedPatient;

    [ObservableProperty]
    private DateTimeOffset? _bookingDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    [ObservableProperty]
    private bool _hasMessage;

    [ObservableProperty]
    private bool _isBusy;

    public NewBookingViewModel(IBookingService bookingService, IPatientService patientService)
    {
        _bookingService = bookingService;
        _patientService = patientService;
    }

    public async Task InitializeAsync()
    {
        await LoadPatientsAsync();
        await RefreshServicesAvailabilityAsync();
    }

    private async Task LoadPatientsAsync()
    {
        try
        {
            var patients = await _patientService.GetAllPatientsAsync();
            Patients.Clear();
            foreach (var p in patients)
            {
                Patients.Add(p);
            }

            if (Patients.Count > 0 && SelectedPatient == null)
            {
                SelectedPatient = Patients[0];
            }
        }
        catch (Exception ex)
        {
            SetMessage($"Failed to load patients: {ex.Message}", false);
        }
    }

    partial void OnBookingDateChanged(DateTimeOffset? value)
    {
        _ = RefreshServicesAvailabilityAsync();
    }

    [RelayCommand]
    public async Task RefreshServicesAvailabilityAsync()
    {
        if (BookingDate == null) return;

        IsBusy = true;
        try
        {
            var availabilities = await _bookingService.GetAvailabilityForDateAsync(BookingDate.Value.DateTime);
            
            // Retain selection if valid
            var previouslySelectedIds = Services.Where(s => s.IsSelected).Select(s => s.ServiceId).ToHashSet();

            Services.Clear();
            foreach (var a in availabilities)
            {
                var item = new SelectableServiceItemViewModel
                {
                    ServiceId = a.ServiceId,
                    Code = a.Code,
                    Name = a.Name,
                    DailyCapacity = a.DailyCapacity,
                    RemainingCapacity = a.RemainingCapacity,
                    IsEnabled = a.IsAvailable,
                    IsSelected = previouslySelectedIds.Contains(a.ServiceId) && a.IsAvailable
                };
                Services.Add(item);
            }
        }
        catch (Exception ex)
        {
            SetMessage($"Error fetching service availability: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SubmitBookingAsync()
    {
        ClearMessage();

        if (SelectedPatient == null)
        {
            SetMessage("Please select an existing patient (BR-01).", false);
            return;
        }

        if (BookingDate == null || BookingDate.Value.Date < DateTime.Today)
        {
            SetMessage($"Booking date cannot be in the past (BR-03). Current date is {DateTime.Today:yyyy-MM-dd}.", false);
            return;
        }

        var selected = Services.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SetMessage("Please select at least 1 diagnostic service (BR-02: 1-3 services allowed).", false);
            return;
        }

        if (selected.Count > 3)
        {
            SetMessage($"You selected {selected.Count} services. Hospital rule (BR-02) limits a booking to a maximum of 3 services.", false);
            return;
        }

        IsBusy = true;
        try
        {
            var request = new BookingRequest
            {
                PatientId = SelectedPatient.Id,
                BookingDate = BookingDate.Value.DateTime,
                SelectedServiceIds = selected.Select(s => s.ServiceId).ToList()
            };

            var result = await _bookingService.CreateBookingAsync(request);

            if (result.IsSuccess && result.Booking != null)
            {
                SetMessage(
                    $"Booking confirmed successfully!\nReference: {result.Booking.ReferenceNumber}\nPatient: {result.Booking.PatientName}\nDate: {result.Booking.BookingDate:yyyy-MM-dd}\nServices: {string.Join(", ", result.Booking.ServiceNames)}",
                    true);

                // Uncheck services
                foreach (var s in Services)
                {
                    s.IsSelected = false;
                }

                // Refresh availability
                await RefreshServicesAvailabilityAsync();

                // Trigger external listeners
                BookingConfirmed?.Invoke();
            }
            else
            {
                SetMessage(result.ErrorMessage ?? "Booking request was rejected by the server.", false);
                await RefreshServicesAvailabilityAsync();
            }
        }
        catch (Exception ex)
        {
            SetMessage($"An unexpected error occurred: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var s in Services)
        {
            s.IsSelected = false;
        }
        ClearMessage();
    }

    private void SetMessage(string message, bool isSuccess)
    {
        StatusMessage = message;
        IsSuccessMessage = isSuccess;
        HasMessage = true;
    }

    private void ClearMessage()
    {
        StatusMessage = null;
        HasMessage = false;
    }
}
