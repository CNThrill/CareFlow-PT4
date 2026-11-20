using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CareFlow.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private NewBookingViewModel _newBooking;

    [ObservableProperty]
    private ServiceAvailabilityViewModel _availability;

    [ObservableProperty]
    private BookingHistoryViewModel _history;

    [ObservableProperty]
    private VerificationViewModel _verification;

    [ObservableProperty]
    private int _selectedTabIndex;

    public MainWindowViewModel(
        NewBookingViewModel newBooking,
        ServiceAvailabilityViewModel availability,
        BookingHistoryViewModel history,
        VerificationViewModel verification)
    {
        NewBooking = newBooking;
        Availability = availability;
        History = history;
        Verification = verification;

        // Subscribe to booking confirmation to auto-refresh other tabs
        NewBooking.BookingConfirmed += OnBookingConfirmed;
    }

    private void OnBookingConfirmed()
    {
        _ = Availability.LoadAvailabilityAsync();
        _ = History.LoadBookingsAsync();
    }

    public async Task InitializeAsync()
    {
        await NewBooking.InitializeAsync();
        await Availability.InitializeAsync();
        await History.InitializeAsync();
    }
}
