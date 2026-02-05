using System.Globalization;
using Wiretap.Maui.Core;
using Wiretap.Maui.UI;

namespace Wiretap.Maui.Services;

/// <summary>
/// Manages the Wiretap entry point (notification-based on supported platforms).
/// </summary>
public sealed partial class WiretapEntryPointService : IDisposable
{
    private const int MaxPreviewLines = 5;
    private readonly IWiretapStore _store;
    private readonly WiretapOptions _options;
    private bool _isVisible;
    private int _lastCount = -1;

    public WiretapEntryPointService(IWiretapStore store, WiretapOptions options)
    {
        _store = store;
        _options = options;

        _store.OnRecordAdded += OnRecordAdded;
        _store.OnRecordsCleared += OnRecordsCleared;
    }

    /// <summary>
    /// Shows the entry point (notification on Android, badge/notification on iOS).
    /// </summary>
    public void Show()
    {
        if (!_options.ShowFloatingButton)
            return;

        _isVisible = true;
        UpdateCount();
    }

    /// <summary>
    /// Hides the entry point.
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        _lastCount = -1;
        HidePlatform();
    }

    private void OnRecordAdded(HttpRecord _)
    {
        UpdateCount();
    }

    private void OnRecordsCleared()
    {
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (!_isVisible)
            return;

        var count = _store.GetRecords().Count;
        if (count == _lastCount)
            return;

        _lastCount = count;
        ShowPlatform(count);
    }

    partial void ShowPlatform(int count);
    partial void HidePlatform();

    private IReadOnlyList<string> BuildPreviewLines()
    {
        var lines = new List<string>();
        var records = _store.GetRecords();

        foreach (var record in records.Take(MaxPreviewLines))
        {
            var line = FormatPreviewLine(record);
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        return lines;
    }

    private static string FormatPreviewLine(HttpRecord record)
    {
        var status = record.IsComplete
            ? record.StatusCode.ToString(CultureInfo.InvariantCulture)
            : record.IsFailed ? "ERR" : "PEND";

        var method = string.IsNullOrWhiteSpace(record.Method)
            ? "?"
            : record.Method.ToUpperInvariant();

        var path = ExtractPath(record.Url);
        if (string.IsNullOrWhiteSpace(path))
            path = record.DisplayUrl;

        if (string.IsNullOrWhiteSpace(path))
            return $"{status} {method}";

        return $"{status} {method} {Truncate(path, 48)}";
    }

    private static string ExtractPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrWhiteSpace(uri.AbsolutePath))
                return uri.AbsolutePath;
            if (!string.IsNullOrWhiteSpace(uri.Host))
                return uri.Host;
        }

        return url;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        if (maxLength <= 3)
            return value[..maxLength];

        return value[..(maxLength - 3)] + "...";
    }

    public void Dispose()
    {
        _store.OnRecordAdded -= OnRecordAdded;
        _store.OnRecordsCleared -= OnRecordsCleared;
        Hide();
    }

    internal static void OpenInspectorFromNotification()
    {
        var services = WiretapServiceLocator.GetServices();
        services?.GetService<WiretapNavigator>()?.Open();
    }
}
