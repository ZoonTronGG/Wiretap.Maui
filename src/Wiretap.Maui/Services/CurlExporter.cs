using System.Text;
using Wiretap.Maui.Core;

namespace Wiretap.Maui.Services;

/// <summary>
/// Exports HTTP records as cURL commands for easy reproduction and sharing.
/// </summary>
public static class CurlExporter
{
    /// <summary>
    /// Converts an HTTP record to a cURL command string.
    /// </summary>
    /// <param name="record">The HTTP record to convert.</param>
    /// <returns>A valid cURL command that reproduces the request.</returns>
    public static string ToCurl(HttpRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var sb = new StringBuilder("curl");

        // Add method (skip for GET as it's the default)
        if (!string.Equals(record.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($" -X {record.Method}");
        }

        // Add headers
        foreach (var (headerName, headerValues) in record.RequestHeaders)
        {
            // Skip pseudo-headers and content-length (curl calculates it)
            if (headerName.StartsWith(':') ||
                string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Handle multi-value headers
            foreach (var value in headerValues)
            {
                var escapedValue = EscapeForShell(value);
                sb.Append($" \\\n  -H '{headerName}: {escapedValue}'");
            }
        }

        // Add body if present
        if (!string.IsNullOrEmpty(record.RequestBody))
        {
            var escapedBody = EscapeForShell(record.RequestBody);
            sb.Append($" \\\n  -d '{escapedBody}'");
        }

        // Add URL (always last, with proper escaping)
        var escapedUrl = EscapeForShell(record.Url);
        sb.Append($" \\\n  '{escapedUrl}'");

        return sb.ToString();
    }

    /// <summary>
    /// Converts an HTTP record to a compact single-line cURL command.
    /// </summary>
    /// <param name="record">The HTTP record to convert.</param>
    /// <returns>A single-line cURL command.</returns>
    public static string ToCurlCompact(HttpRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var sb = new StringBuilder("curl");

        // Add method
        if (!string.Equals(record.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($" -X {record.Method}");
        }

        // Add headers
        foreach (var (headerName, headerValues) in record.RequestHeaders)
        {
            if (headerName.StartsWith(':') ||
                string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in headerValues)
            {
                var escapedValue = EscapeForShell(value);
                sb.Append($" -H '{headerName}: {escapedValue}'");
            }
        }

        // Add body
        if (!string.IsNullOrEmpty(record.RequestBody))
        {
            var escapedBody = EscapeForShell(record.RequestBody);
            sb.Append($" -d '{escapedBody}'");
        }

        // Add URL
        var escapedUrl = EscapeForShell(record.Url);
        sb.Append($" '{escapedUrl}'");

        return sb.ToString();
    }

#if MAUI
    /// <summary>
    /// Copies the cURL command to the system clipboard.
    /// </summary>
    /// <param name="record">The HTTP record to convert and copy.</param>
    /// <returns>A task that completes when the copy operation is done.</returns>
    public static async Task CopyToClipboardAsync(HttpRecord record)
    {
        var curl = ToCurl(record);
        await Clipboard.SetTextAsync(curl);
    }
#endif

    /// <summary>
    /// Escapes a string for safe use in a shell single-quoted context.
    /// In single quotes, the only character that needs escaping is the single quote itself.
    /// We do this by ending the single-quoted string, adding an escaped quote, and starting a new single-quoted string.
    /// Example: "it's" becomes "it'\''s"
    /// </summary>
    private static string EscapeForShell(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // In single quotes, only single quote needs escaping
        // 'foo'\''bar' = foo'bar
        return value.Replace("'", "'\\''");
    }
}
