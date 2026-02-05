# Wiretap.Maui

In-app HTTP traffic inspector for .NET MAUI - debug network requests like [Chucker](https://github.com/ChuckerTeam/chucker) for Android.

[![NuGet](https://img.shields.io/nuget/v/Wiretap.Maui.svg)](https://www.nuget.org/packages/Wiretap.Maui/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- 📡 **Intercept HTTP traffic** - Captures all requests/responses from your HttpClient
- 🎯 **Zero-config setup** - Just two lines of code to integrate
- 📋 **Request list** - View all captured requests with method, status, duration, size
- 🔍 **Detail view** - Full request/response headers and bodies with tabs
- 🔎 **Search & Filter** - Filter by method (GET, POST, etc.), status code (2xx, 4xx, 5xx), or search text
- 🎨 **JSON formatting** - Pretty-printed JSON bodies for easy reading
- 🔒 **Sensitive data masking** - Auto-masks Authorization headers, API keys, cookies
- 📤 **Export to cURL** - Copy request as cURL command
- 📄 **Export to PDF** - Generate professional PDF reports
- 📋 **Copy to clipboard** - Copy headers or body with one tap
- 💾 **SQLite persistence** - Optional persistent storage across app restarts
- 🔔 **Notifications** - Android/iOS notifications for quick access to inspector
- 🌙 **Dark mode support** - Adapts to system theme
- ⚡ **Debug-only** - Easily exclude from release builds with `#if DEBUG`
- 🔄 **Ring buffer storage** - Configurable limit, no memory bloat

## Installation

```bash
dotnet add package Wiretap.Maui
```

Or via NuGet Package Manager:
```
Install-Package Wiretap.Maui
```

## Quick Start

### Step 1: Add Wiretap to your MauiProgram.cs

```csharp
using Wiretap.Maui;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
#if DEBUG
        .UseWiretap()  // Add this line!
#endif
        ;

    return builder.Build();
}
```

### Step 2: Add the handler to your HttpClients

```csharp
builder.Services.AddHttpClient("MyApi", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/");
})
#if DEBUG
.AddWiretapHandler()  // Add this line!
#endif
;
```

That's it!

## ⚠️ Important Notes

### Handler Order Matters

Always add `WiretapHandler` **AFTER** your auth handlers so it captures requests WITH authorization headers:

```csharp
builder.Services.AddHttpClient<ApiService>(...)
    .AddHttpMessageHandler<AuthTokenHandler>()  // First: adds auth token
#if DEBUG
    .AddWiretapHandler()                         // Second: captures request with token
#endif
    .AddStandardResilienceHandler();
```

### Don't Use with OAuth/Auth Clients

**Do NOT** add `WiretapHandler` to HttpClients used for OAuth token exchange - it can interfere with the authentication flow:

```csharp
// ❌ DON'T do this for auth clients
builder.Services.AddHttpClient<IAuthService, AuthService>(...)
    .AddWiretapHandler()  // This breaks OAuth flow!

// ✅ Only add to API clients, not auth clients
builder.Services.AddHttpClient<ApiService>(...)
    .AddHttpMessageHandler<AuthTokenHandler>()
#if DEBUG
    .AddWiretapHandler()  // OK here - this is for API calls
#endif
```

### Accessing the Inspector

The recommended way to access the HTTP inspector is via manual navigation (add a button in your Settings or Debug menu):

```csharp
// Add to your Settings page ViewModel
[RelayCommand]
private async Task OpenHttpInspectorAsync()
{
    await Shell.Current.GoToAsync("WiretapPage");
}
```

## Configuration

Customize Wiretap behavior with options:

```csharp
.UseWiretap(options =>
{
    options.MaxStoredRequests = 500;        // Max requests to keep (default: 500)
    options.ShowFloatingButton = true;      // Show floating button (default: true)
    options.PrettyPrintJson = true;         // Format JSON bodies (default: true)
    options.MaskSensitiveHeaders = true;    // Mask auth headers (default: true)
    options.CaptureRequestHeaders = true;   // Capture request headers (default: true)
    options.CaptureResponseHeaders = true;  // Capture response headers (default: true)
    options.MaxBodySize = 1_048_576;        // Max body size in bytes (default: 1MB)

    // Custom sensitive header patterns
    options.SensitiveHeaderPatterns = new[]
    {
        "Authorization", "X-Api-Key", "Cookie", "Set-Cookie", "X-Auth-Token"
    };
})
```

## Show/Hide Overlay Programmatically

> ⚠️ **Note:** The floating overlay may cause issues with touch handling and navigation on some pages. We recommend using manual navigation instead (see below).

```csharp
// In your App.xaml.cs or anywhere with access to Application

#if DEBUG
// Show the overlay
this.ShowWiretapOverlay();

// Hide the overlay
this.HideWiretapOverlay();
#endif

// Or using IServiceProvider
serviceProvider.ShowWiretapOverlay();
```

## Navigate to Inspector Directly (Recommended)

Add a button to your Settings or Profile page for easy access:

**ViewModel:**
```csharp
public partial class SettingsViewModel : ObservableObject
{
#if DEBUG
    public bool IsDebugMode => true;

    [RelayCommand]
    private async Task OpenHttpInspectorAsync()
    {
        await Shell.Current.GoToAsync("WiretapPage");
    }
#else
    public bool IsDebugMode => false;
#endif
}
```

**XAML:**
```xml
<!-- Developer Tools section (only visible in DEBUG) -->
<VerticalStackLayout IsVisible="{Binding IsDebugMode}">
    <Label Text="DEVELOPER TOOLS" Style="{StaticResource SectionHeaderStyle}" />

    <Button Text="📡 HTTP Inspector"
            Command="{Binding OpenHttpInspectorCommand}"
            BackgroundColor="#2196F3"
            TextColor="White" />
</VerticalStackLayout>
```

**Programmatic navigation:**
```csharp
// Using Shell navigation (routes are auto-registered)
await Shell.Current.GoToAsync("WiretapPage");

// Or push as modal
var store = serviceProvider.GetService<IWiretapStore>();
var page = new WiretapPage(store, Application.Current.Dispatcher);
await Navigation.PushModalAsync(new NavigationPage(page));
```

## Supported Platforms

| Platform | Minimum Version |
|----------|-----------------|
| iOS | 15.0+ |
| Mac Catalyst | 15.0+ |
| Android | API 24+ (Android 7.0) |

## How It Works

Wiretap uses a `DelegatingHandler` to intercept HTTP traffic in the `HttpClient` pipeline:

```
Your App → WiretapHandler → AuthHandler → Network
              ↓
         WiretapStore (ring buffer)
              ↓
         WiretapPage (UI)
```

**Key points:**
- Request/response bodies are **cloned** (not consumed), so your API calls work normally
- Records are stored in memory with a configurable limit (oldest removed when full)
- The floating button provides quick access to the inspector UI
- Sensitive headers are masked by default to protect credentials

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Your MAUI App                      │
│  ┌─────────────┐    ┌──────────────────┐            │
│  │ MauiProgram │───▶│ .UseWiretap()    │            │
│  └─────────────┘    └──────────────────┘            │
│         │                                            │
│         ▼                                            │
│  ┌─────────────────────────────────────┐            │
│  │         HttpClient Pipeline         │            │
│  │  ┌───────────────┐ ┌─────────────┐  │            │
│  │  │WiretapHandler │▶│ YourHandler │  │            │
│  │  └───────────────┘ └─────────────┘  │            │
│  └─────────────────────────────────────┘            │
└─────────────────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────┐
│               Wiretap.Maui Package                   │
│                                                      │
│  ┌──────────────────┐     ┌─────────────────────┐   │
│  │  WiretapHandler  │────▶│   IWiretapStore     │   │
│  │ (DelegatingHandler)    │   (Ring Buffer)     │   │
│  └──────────────────┘     └──────────┬──────────┘   │
│                                      │              │
│  ┌──────────────────┐     ┌──────────▼──────────┐   │
│  │ WiretapOverlay   │────▶│   WiretapPage       │   │
│  │ (Floating Button)│     │   (Request List)    │   │
│  └──────────────────┘     └──────────┬──────────┘   │
│                                      │              │
│                           ┌──────────▼──────────┐   │
│                           │ WiretapDetailPage   │   │
│                           │ (Headers + Body)    │   │
│                           └─────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

## Debug-Only Best Practice

Always wrap Wiretap integration in `#if DEBUG` to ensure it's excluded from release builds:

```csharp
#if DEBUG
    .UseWiretap()
#endif

// and

#if DEBUG
    .AddWiretapHandler()
#endif
```

## Sample App

See the [samples/Wiretap.Maui.Sample](samples/Wiretap.Maui.Sample) folder for a complete working example.

Run it with:
```bash
cd samples/Wiretap.Maui.Sample
dotnet build -t:Run -f net10.0-ios
```

## API Reference

### WiretapExtensions

| Method | Description |
|--------|-------------|
| `UseWiretap(options?)` | Adds Wiretap services to the MAUI app |
| `AddWiretapHandler()` | Adds the HTTP interception handler to HttpClient |
| `ShowWiretapOverlay()` | Shows the floating overlay button |
| `HideWiretapOverlay()` | Hides the floating overlay button |

### WiretapOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxStoredRequests` | `int` | 500 | Maximum number of requests to keep |
| `ShowFloatingButton` | `bool` | true | Whether to show the floating button |
| `PrettyPrintJson` | `bool` | true | Format JSON in detail view |
| `MaskSensitiveHeaders` | `bool` | true | Mask sensitive header values |
| `CaptureRequestHeaders` | `bool` | true | Capture request headers |
| `CaptureResponseHeaders` | `bool` | true | Capture response headers |
| `MaxBodySize` | `int` | 1MB | Max body size to capture |
| `SensitiveHeaderPatterns` | `string[]` | See below | Headers to mask |

**Default sensitive headers:** `Authorization`, `X-Api-Key`, `Cookie`, `Set-Cookie`

### IWiretapStore

For programmatic access to captured requests:

```csharp
public interface IWiretapStore
{
    IReadOnlyList<HttpRecord> GetRecords();
    HttpRecord? GetRecord(Guid id);
    void Clear();
    event Action<HttpRecord>? OnRecordAdded;
    event Action? OnRecordsCleared;
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by [Chucker](https://github.com/ChuckerTeam/chucker) for Android
- Project structure based on [Plugin.Maui.Feature](https://github.com/jfversluis/Plugin.Maui.Feature) template by Gerald Versluis

## Roadmap

- [x] HTTP traffic interception
- [x] Request list and detail UI
- [x] JSON pretty-printing
- [x] Sensitive header masking
- [x] Export to cURL format
- [x] Export to PDF format
- [x] Request filtering and search
- [x] SQLite persistent storage
- [x] Notifications (Android/iOS)
- [x] Dark mode support
