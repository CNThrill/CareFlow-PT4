using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CareFlow.DTOs;
using CareFlow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CareFlow.ViewModels;

public partial class ServiceAvailabilityViewModel : ViewModelBase
{
    private readonly IBookingService _bookingService;

    public ObservableCollection<ServiceAvailabilityDto> Services { get; } = new();

    [ObservableProperty]
    private DateTimeOffset? _selectedDate = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ServiceAvailabilityViewModel(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public async Task InitializeAsync()
    {
        await LoadAvailabilityAsync();
    }

    partial void OnSelectedDateChanged(DateTimeOffset? value)
    {
        _ = LoadAvailabilityAsync();
    }

    [RelayCommand]
    public async Task LoadAvailabilityAsync()
    {
        if (SelectedDate == null) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var results = await _bookingService.GetAvailabilityForDateAsync(SelectedDate.Value.DateTime);
            Services.Clear();
            foreach (var item in results)
            {
                Services.Add(item);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load service availability: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
