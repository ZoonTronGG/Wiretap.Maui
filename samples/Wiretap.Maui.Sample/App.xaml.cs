namespace Wiretap.Maui.Sample;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = _serviceProvider.GetRequiredService<MainPage>();
        var window = new Window(mainPage);

        // Show the Wiretap overlay after the window is ready
        window.Created += (s, e) =>
        {
#if DEBUG
            // Show floating button after a short delay to ensure UI is ready
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                _serviceProvider.ShowWiretapOverlay();
            });
#endif
        };

        return window;
    }
}
