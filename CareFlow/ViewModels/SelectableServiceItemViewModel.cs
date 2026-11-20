using CommunityToolkit.Mvvm.ComponentModel;

namespace CareFlow.ViewModels;

public partial class SelectableServiceItemViewModel : ObservableObject
{
    public int ServiceId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int DailyCapacity { get; init; }

    [ObservableProperty]
    private int _remainingCapacity;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled = true;

    public string DisplayText => $"{Code} - {Name} ({RemainingCapacity} / {DailyCapacity} left)";
}
