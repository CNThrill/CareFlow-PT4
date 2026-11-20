using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CareFlow.DTOs;
using CareFlow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CareFlow.ViewModels;

public partial class BookingHistoryViewModel : ViewModelBase
{
    private readonly IBookingService _bookingService;

    public ObservableCollection<BookingDetailDto> Bookings { get; } = new();

    [ObservableProperty]
    private BookingDetailDto? _selectedBooking;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    public BookingHistoryViewModel(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public async Task InitializeAsync()
    {
        await LoadBookingsAsync();
    }

    [RelayCommand]
    public async Task LoadBookingsAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            var list = await _bookingService.GetAllBookingsAsync();
            Bookings.Clear();
            foreach (var b in list)
            {
                Bookings.Add(b);
            }

            if (Bookings.Count > 0 && SelectedBooking == null)
            {
                SelectedBooking = Bookings[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to retrieve bookings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
