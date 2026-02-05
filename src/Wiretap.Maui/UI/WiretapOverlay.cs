using Microsoft.Maui.Controls.Shapes;
using Wiretap.Maui.Core;

namespace Wiretap.Maui.UI;

/// <summary>
/// Floating button view for Wiretap HTTP inspector.
/// Uses a Border-based approach that works reliably across platforms.
/// </summary>
public class WiretapFloatingButton : ContentView
{
    private readonly IWiretapStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly Label _badgeLabel;
    private readonly Border _badgeBorder;

    private const double ButtonSize = 56;
    private const double BadgeSize = 20;

    public WiretapFloatingButton(IWiretapStore store, IServiceProvider serviceProvider)
    {
        _store = store;
        _serviceProvider = serviceProvider;

        // Set explicit size so we don't expand and block touches
        WidthRequest = ButtonSize + 10;
        HeightRequest = ButtonSize + 10;
        InputTransparent = false;

        // Badge label
        _badgeLabel = new Label
        {
            Text = "0",
            TextColor = Colors.White,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        // Badge border (circular)
        _badgeBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F44336"),
            StrokeShape = new Ellipse(),
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            Padding = 0,
            WidthRequest = BadgeSize,
            HeightRequest = BadgeSize,
            Content = _badgeLabel,
            IsVisible = false
        };

        // Main button (circular)
        var buttonBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#2196F3"),
            StrokeShape = new Ellipse(),
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            Padding = 0,
            WidthRequest = ButtonSize,
            HeightRequest = ButtonSize,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Offset = new Point(2, 4),
                Radius = 8,
                Opacity = 0.3f
            },
            Content = new Label
            {
                Text = "📡",
                FontSize = 24,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        // Add tap gesture
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnButtonTapped;
        buttonBorder.GestureRecognizers.Add(tapGesture);

        var absoluteLayout = new AbsoluteLayout
        {
            WidthRequest = ButtonSize + 10,
            HeightRequest = ButtonSize + 10
        };
        absoluteLayout.Add(buttonBorder);
        AbsoluteLayout.SetLayoutBounds(buttonBorder, new Rect(0, 10, ButtonSize, ButtonSize));
        absoluteLayout.Add(_badgeBorder);
        AbsoluteLayout.SetLayoutBounds(_badgeBorder, new Rect(ButtonSize - BadgeSize / 2, 0, BadgeSize, BadgeSize));

        Content = absoluteLayout;

        // Subscribe to store changes
        _store.OnRecordAdded += _ => UpdateBadge();
        _store.OnRecordsCleared += () => UpdateBadge();

        UpdateBadge();
    }

    private void UpdateBadge()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var count = _store.GetRecords().Count;
            _badgeBorder.IsVisible = count > 0;
            _badgeLabel.Text = count > 99 ? "99+" : count.ToString();
        });
    }

    private async void OnButtonTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            // Try Shell navigation first
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(WiretapPage));
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Wiretap: Shell navigation failed - {ex.Message}");
        }

        // Fallback: Create and push page manually
        try
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            var navigation = window?.Page?.Navigation;

            if (navigation != null)
            {
                var store = _serviceProvider.GetService<IWiretapStore>();

                if (store != null)
                {
                    var page = new WiretapPage(store, Application.Current!.Dispatcher);
                    await navigation.PushModalAsync(new NavigationPage(page));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Wiretap: Navigation failed - {ex.Message}");
        }
    }
}

/// <summary>
/// Service for managing the Wiretap floating button overlay.
/// </summary>
public class WiretapOverlayService : IDisposable
{
    private readonly WiretapOptions _options;
    private readonly IWiretapStore _store;
    private readonly IServiceProvider _serviceProvider;
    private WiretapFloatingButton? _button;
    private bool _isAttached;

    public WiretapOverlayService(IWiretapStore store, WiretapOptions options, IServiceProvider serviceProvider)
    {
        _store = store;
        _options = options;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Shows the floating overlay button.
    /// </summary>
    public void Show()
    {
        if (!_options.ShowFloatingButton) return;

        if (!_isAttached)
        {
            AttachButton();
            _isAttached = true;
        }

        if (_button != null)
        {
            _button.IsVisible = true;
        }
    }

    private void AttachButton()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        var page = window?.Page;

        System.Diagnostics.Debug.WriteLine($"Wiretap: AttachButton called");
        System.Diagnostics.Debug.WriteLine($"Wiretap: Window = {window?.GetType().Name ?? "null"}");
        System.Diagnostics.Debug.WriteLine($"Wiretap: Page = {page?.GetType().Name ?? "null"}");

        if (page == null)
        {
            System.Diagnostics.Debug.WriteLine("Wiretap: No page found to attach overlay");
            return;
        }

        _button = new WiretapFloatingButton(_store, _serviceProvider);

        // Find the target page to attach button
        Page? targetPage = page;

        if (page is Shell shell)
        {
            System.Diagnostics.Debug.WriteLine($"Wiretap: Shell detected, CurrentPage = {shell.CurrentPage?.GetType().Name ?? "null"}");
            targetPage = shell.CurrentPage;
        }
        else if (page is NavigationPage navPage)
        {
            System.Diagnostics.Debug.WriteLine($"Wiretap: NavigationPage detected, CurrentPage = {navPage.CurrentPage?.GetType().Name ?? "null"}");
            targetPage = navPage.CurrentPage;
        }

        if (targetPage != null)
        {
            AddButtonToPage(targetPage);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Wiretap: Could not find target page for button");
        }
    }

    private void AddButtonToPage(Page page)
    {
        System.Diagnostics.Debug.WriteLine($"Wiretap: AddButtonToPage called with {page.GetType().Name}");

        if (_button == null)
        {
            System.Diagnostics.Debug.WriteLine("Wiretap: Button is null");
            return;
        }

        if (page is not ContentPage contentPage)
        {
            System.Diagnostics.Debug.WriteLine($"Wiretap: Cannot attach to page type {page.GetType().Name} - not a ContentPage");
            return;
        }

        var existingContent = contentPage.Content;
        System.Diagnostics.Debug.WriteLine($"Wiretap: Existing content = {existingContent?.GetType().Name ?? "null"}");

        if (existingContent == null)
        {
            System.Diagnostics.Debug.WriteLine("Wiretap: Page has no content to wrap");
            return;
        }

        // Position button at bottom-right
        _button.HorizontalOptions = LayoutOptions.End;
        _button.VerticalOptions = LayoutOptions.End;
        _button.Margin = new Thickness(0, 0, 20, 100);
        _button.InputTransparent = false;

        // Try to add button directly to existing layout if it's a Grid
        if (existingContent is Grid existingGrid)
        {
            // Make button span all rows and columns so it floats on top
            if (existingGrid.RowDefinitions.Count > 0)
                Grid.SetRowSpan(_button, existingGrid.RowDefinitions.Count);
            if (existingGrid.ColumnDefinitions.Count > 0)
                Grid.SetColumnSpan(_button, existingGrid.ColumnDefinitions.Count);

            existingGrid.Add(_button);
            System.Diagnostics.Debug.WriteLine($"Wiretap: ✅ Button added directly to existing Grid (rows={existingGrid.RowDefinitions.Count}, cols={existingGrid.ColumnDefinitions.Count})");
            return;
        }

        // For other layouts, wrap in Grid
        var wrapperGrid = new Grid();

        // Move existing content to wrapper
        contentPage.Content = null; // Detach first
        wrapperGrid.Add(existingContent);
        wrapperGrid.Add(_button);

        contentPage.Content = wrapperGrid;
        System.Diagnostics.Debug.WriteLine($"Wiretap: ✅ Button attached via wrapper Grid to {page.GetType().Name}");
    }

    /// <summary>
    /// Hides the floating overlay button.
    /// </summary>
    public void Hide()
    {
        if (_button != null)
        {
            _button.IsVisible = false;
        }
    }

    /// <summary>
    /// Toggles the visibility of the floating overlay button.
    /// </summary>
    public void Toggle()
    {
        if (_button == null || !_button.IsVisible)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void Dispose()
    {
        // Button will be disposed with the page
        _button = null;
    }
}
