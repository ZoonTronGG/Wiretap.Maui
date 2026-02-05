using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Wiretap.Maui.Core;

namespace Wiretap.Maui.UI;

/// <summary>
/// Navigates to the Wiretap inspector page.
/// </summary>
internal sealed class WiretapNavigator
{
    private readonly IServiceProvider _services;

    public WiretapNavigator(IServiceProvider services)
    {
        _services = services;
    }

    public void Open()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(nameof(WiretapPage));
                    return;
                }
            }
            catch
            {
                // Ignore Shell navigation errors and fallback below.
            }

            try
            {
                var window = Application.Current?.Windows.FirstOrDefault();
                var navigation = window?.Page?.Navigation;

                if (navigation == null)
                    return;

                var page = _services.GetService<WiretapPage>();
                if (page == null)
                {
                    var store = _services.GetService<IWiretapStore>();
                    if (store == null || Application.Current == null)
                        return;

                    page = new WiretapPage(store, Application.Current.Dispatcher);
                }

                await navigation.PushModalAsync(new NavigationPage(page));
            }
            catch
            {
                // Ignore navigation errors.
            }
        });
    }
}
