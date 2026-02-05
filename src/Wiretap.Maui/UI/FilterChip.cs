using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Wiretap.Maui.UI;

/// <summary>
/// Represents a toggleable filter chip for method or status filtering.
/// </summary>
public partial class FilterChip : ObservableObject
{
    /// <summary>
    /// Display text for the chip (e.g., "GET", "POST", "2xx").
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The value used for filtering (e.g., method name or status group number).
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Color when chip is selected.
    /// </summary>
    public Color SelectedColor { get; init; } = Colors.Blue;

    /// <summary>
    /// Color when chip is not selected.
    /// </summary>
    public Color UnselectedColor { get; init; } = Colors.Gray;

    /// <summary>
    /// Whether the chip is currently selected.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(CurrentTextColor))]
    [NotifyPropertyChangedFor(nameof(CurrentBorderColor))]
    private bool _isSelected;

    /// <summary>
    /// Current background color based on selection state.
    /// </summary>
    public Color CurrentBackgroundColor => IsSelected ? SelectedColor : Colors.Transparent;

    /// <summary>
    /// Current text color based on selection state.
    /// </summary>
    public Color CurrentTextColor => IsSelected ? Colors.White : SelectedColor;

    /// <summary>
    /// Current border color based on selection state.
    /// </summary>
    public Color CurrentBorderColor => SelectedColor;

    /// <summary>
    /// Command to toggle this chip.
    /// </summary>
    public IRelayCommand<FilterChip>? ToggleCommand { get; set; }
}
