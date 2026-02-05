using Wiretap.Maui.Core;

namespace Wiretap.Maui.UI;

/// <summary>
/// Page displaying the list of captured HTTP requests.
/// </summary>
public partial class WiretapPage : ContentPage
{
    private readonly WiretapViewModel _viewModel;

    public WiretapPage(IWiretapStore store, IDispatcher dispatcher)
    {
        InitializeComponent();
        _viewModel = new WiretapViewModel(store, dispatcher);
        BindingContext = _viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Note: We don't dispose the ViewModel here because the store events
        // should continue to update the list even when the page is not visible
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        // Try to close modal first, then pop navigation
        if (Navigation.ModalStack.Count > 0)
        {
            await Navigation.PopModalAsync();
        }
        else if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
        }
        else if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
