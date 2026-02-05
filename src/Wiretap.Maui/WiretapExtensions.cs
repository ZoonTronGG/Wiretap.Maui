using Microsoft.Maui.LifecycleEvents;
using Wiretap.Maui.Core;
using Wiretap.Maui.Database;
using Wiretap.Maui.Handler;
using Wiretap.Maui.Services;
using Wiretap.Maui.UI;

namespace Wiretap.Maui;

/// <summary>
/// Extension methods for integrating Wiretap into a MAUI application.
/// </summary>
public static class WiretapExtensions
{
    /// <summary>
    /// Adds Wiretap HTTP inspector services to the MAUI application.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <param name="configure">Optional configuration action for Wiretap options.</param>
    /// <returns>The MAUI app builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = MauiApp.CreateBuilder();
    /// builder
    ///     .UseMauiApp&lt;App&gt;()
    /// #if DEBUG
    ///     .UseWiretap()
    /// #endif
    ///     ;
    /// </code>
    /// </example>
    public static MauiAppBuilder UseWiretap(
        this MauiAppBuilder builder,
        Action<WiretapOptions>? configure = null)
    {
        var options = new WiretapOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);

        // Register appropriate store based on persistence setting
        if (options.EnablePersistence)
        {
            builder.Services.AddSingleton<HybridWiretapStore>();
            builder.Services.AddSingleton<IWiretapStore>(sp => sp.GetRequiredService<HybridWiretapStore>());
        }
        else
        {
            builder.Services.AddSingleton<IWiretapStore, WiretapStore>();
        }

        builder.Services.AddTransient<WiretapHandler>();

        // Register UI pages
        builder.Services.AddTransient<WiretapPage>();
        builder.Services.AddTransient<WiretapDetailPage>();

        builder.Services.AddSingleton<WiretapNavigator>();
        builder.Services.AddSingleton<WiretapEntryPointService>();

        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android
                .OnCreate((activity, _) => WiretapEntryPointService.TryHandleIntent(activity.Intent))
                .OnNewIntent((_, intent) => WiretapEntryPointService.TryHandleIntent(intent)));
#endif
        });

        // Register routes for Shell navigation
        Routing.RegisterRoute(nameof(WiretapPage), typeof(WiretapPage));
        Routing.RegisterRoute(nameof(WiretapDetailPage), typeof(WiretapDetailPage));

        return builder;
    }

    /// <summary>
    /// Adds the Wiretap HTTP handler to the HttpClient pipeline.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The HTTP client builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddHttpClient&lt;MyApiService&gt;()
    /// #if DEBUG
    ///     .AddWiretapHandler()
    /// #endif
    ///     ;
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddWiretapHandler(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<WiretapHandler>();
    }

    /// <summary>
    /// Shows the Wiretap entry point (notification on Android).
    /// Call this after the app has started and the main page is displayed.
    /// </summary>
    /// <param name="app">The MAUI application.</param>
    /// <example>
    /// <code>
    /// // In App.xaml.cs OnStart() or after main page is loaded:
    /// #if DEBUG
    /// this.ShowWiretapEntryPoint();
    /// #endif
    /// </code>
    /// </example>
    public static void ShowWiretapEntryPoint(this Application app)
    {
        var service = app.Handler?.MauiContext?.Services.GetService<WiretapEntryPointService>();
        service?.Show();
    }

    /// <summary>
    /// Hides the Wiretap entry point.
    /// </summary>
    /// <param name="app">The MAUI application.</param>
    public static void HideWiretapEntryPoint(this Application app)
    {
        var service = app.Handler?.MauiContext?.Services.GetService<WiretapEntryPointService>();
        service?.Hide();
    }

    /// <summary>
    /// Initializes Wiretap persistence. Call this during app startup if using persistence.
    /// This loads previously stored records from the database into memory.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the initialization.</returns>
    public static async Task InitializeWiretapAsync(this IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetService<HybridWiretapStore>();
        if (store != null)
        {
            await store.InitializeAsync();
        }
    }

    /// <summary>
    /// Shows the Wiretap entry point using the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public static void ShowWiretapEntryPoint(this IServiceProvider serviceProvider)
    {
        var service = serviceProvider.GetService<WiretapEntryPointService>();
        service?.Show();
    }

    /// <summary>
    /// Hides the Wiretap entry point using the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public static void HideWiretapEntryPoint(this IServiceProvider serviceProvider)
    {
        var service = serviceProvider.GetService<WiretapEntryPointService>();
        service?.Hide();
    }

    /// <summary>
    /// Shows the Wiretap entry point.
    /// </summary>
    /// <param name="app">The MAUI application.</param>
    [Obsolete("Floating overlay removed. Use ShowWiretapEntryPoint instead.")]
    public static void ShowWiretapOverlay(this Application app) => app.ShowWiretapEntryPoint();

    /// <summary>
    /// Hides the Wiretap entry point.
    /// </summary>
    /// <param name="app">The MAUI application.</param>
    [Obsolete("Floating overlay removed. Use HideWiretapEntryPoint instead.")]
    public static void HideWiretapOverlay(this Application app) => app.HideWiretapEntryPoint();

    /// <summary>
    /// Shows the Wiretap entry point using the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    [Obsolete("Floating overlay removed. Use ShowWiretapEntryPoint instead.")]
    public static void ShowWiretapOverlay(this IServiceProvider serviceProvider) => serviceProvider.ShowWiretapEntryPoint();

    /// <summary>
    /// Hides the Wiretap entry point using the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    [Obsolete("Floating overlay removed. Use HideWiretapEntryPoint instead.")]
    public static void HideWiretapOverlay(this IServiceProvider serviceProvider) => serviceProvider.HideWiretapEntryPoint();
}
