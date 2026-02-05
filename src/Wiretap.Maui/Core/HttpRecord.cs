using System.Globalization;

namespace Wiretap.Maui.Core;

/// <summary>
/// Represents a captured HTTP request/response pair.
/// </summary>
public class HttpRecord
{
    /// <summary>
    /// Unique identifier for this record.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the request was initiated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Local timestamp for UI display.
    /// </summary>
    public DateTime LocalTimestamp
    {
        get
        {
            var timestamp = Timestamp;
            if (timestamp.Kind == DateTimeKind.Unspecified)
                timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            return timestamp.Kind == DateTimeKind.Local ? timestamp : timestamp.ToLocalTime();
        }
    }

    /// <summary>
    /// Total time from request sent to response received.
    /// </summary>
    public TimeSpan Duration { get; set; }

    // Request properties
    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Full request URL including query string.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Request headers as key-value pairs (values can be arrays for multi-value headers).
    /// </summary>
    public Dictionary<string, string[]> RequestHeaders { get; init; } = new();

    /// <summary>
    /// Request body content (may be null for GET requests).
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// Request body size in bytes.
    /// </summary>
    public long RequestSize { get; set; }

    // Response properties
    /// <summary>
    /// HTTP status code (e.g., 200, 404, 500).
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// HTTP reason phrase (e.g., "OK", "Not Found").
    /// </summary>
    public string? ReasonPhrase { get; set; }

    /// <summary>
    /// Response headers as key-value pairs.
    /// </summary>
    public Dictionary<string, string[]> ResponseHeaders { get; set; } = new();

    /// <summary>
    /// Response body content.
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Response body size in bytes.
    /// </summary>
    public long ResponseSize { get; set; }

    // State properties
    /// <summary>
    /// Whether the request/response cycle completed successfully.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the request body was truncated due to size limits.
    /// </summary>
    public bool RequestBodyTruncated { get; set; }

    /// <summary>
    /// Whether the response body was truncated due to size limits.
    /// </summary>
    public bool ResponseBodyTruncated { get; set; }

    // Computed properties for UI display

    /// <summary>
    /// Whether the response indicates success (2xx status code).
    /// </summary>
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    /// <summary>
    /// Whether the response indicates a redirect (3xx status code).
    /// </summary>
    public bool IsRedirect => StatusCode >= 300 && StatusCode < 400;

    /// <summary>
    /// Whether the response indicates a client error (4xx status code).
    /// </summary>
    public bool IsClientError => StatusCode >= 400 && StatusCode < 500;

    /// <summary>
    /// Whether the response indicates a server error (5xx status code).
    /// </summary>
    public bool IsServerError => StatusCode >= 500;

    /// <summary>
    /// Whether the request failed (no response received).
    /// </summary>
    public bool IsFailed => !IsComplete && !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Total size of request + response in bytes.
    /// </summary>
    public long TotalSize => RequestSize + ResponseSize;

    /// <summary>
    /// Short display URL (host + path, no query string).
    /// </summary>
    public string DisplayUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Url)) return string.Empty;
            try
            {
                var uri = new Uri(Url);
                var path = uri.AbsolutePath;
                if (path.Length > 50)
                    path = path[..47] + "...";
                return $"{uri.Host}{path}";
            }
            catch
            {
                return Url.Length > 60 ? Url[..57] + "..." : Url;
            }
        }
    }

    /// <summary>
    /// Duration formatted for display (e.g., "123 ms", "1.5 s").
    /// </summary>
    public string DurationDisplay
    {
        get
        {
            if (Duration.TotalMilliseconds < 1000)
                return string.Format(CultureInfo.InvariantCulture, "{0:F0} ms", Duration.TotalMilliseconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} s", Duration.TotalSeconds);
        }
    }
}
