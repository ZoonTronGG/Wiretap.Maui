using System.Globalization;

namespace Wiretap.Maui.UI.Converters;

/// <summary>
/// Converts HTTP status codes to colors for visual indication.
/// 2xx = Green (success), 3xx = Blue (redirect), 4xx = Orange (client error), 5xx = Red (server error)
/// </summary>
public class StatusCodeToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int statusCode)
            return Colors.Gray;

        return statusCode switch
        {
            >= 200 and < 300 => Colors.Green,      // Success
            >= 300 and < 400 => Colors.DodgerBlue, // Redirect
            >= 400 and < 500 => Colors.Orange,     // Client error
            >= 500 => Colors.Red,                   // Server error
            _ => Colors.Gray                        // Unknown/pending
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts HTTP method to a background color for the method badge.
/// </summary>
public class MethodToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string method)
            return Colors.Gray;

        return method.ToUpperInvariant() switch
        {
            "GET" => Color.FromArgb("#2196F3"),     // Blue
            "POST" => Color.FromArgb("#4CAF50"),    // Green
            "PUT" => Color.FromArgb("#FF9800"),     // Orange
            "PATCH" => Color.FromArgb("#9C27B0"),   // Purple
            "DELETE" => Color.FromArgb("#F44336"),  // Red
            "HEAD" => Color.FromArgb("#607D8B"),    // Blue Gray
            "OPTIONS" => Color.FromArgb("#795548"), // Brown
            _ => Colors.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean IsComplete + ErrorMessage to an error indicator visibility.
/// </summary>
public class FailedToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Expects the HttpRecord itself
        if (value is Core.HttpRecord record)
        {
            return record.IsFailed;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}
