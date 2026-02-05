using System.Text.Json;
using SQLite;
using Wiretap.Maui.Core;

namespace Wiretap.Maui.Database;

/// <summary>
/// SQLite entity representing a captured HTTP record.
/// Headers are stored as JSON strings for efficient storage.
/// </summary>
[Table("HttpRecords")]
public class HttpRecordEntity
{
    /// <summary>
    /// Primary key - stored as string for SQLite compatibility.
    /// </summary>
    [PrimaryKey]
    [Column("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// When the request was initiated.
    /// </summary>
    [Indexed]
    [Column("Timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    [Column("DurationMs")]
    public long DurationMs { get; set; }

    // Request properties

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    [Indexed]
    [Column("Method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Full request URL including query string.
    /// </summary>
    [Indexed]
    [Column("Url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Request headers serialized as JSON.
    /// </summary>
    [Column("RequestHeadersJson")]
    public string? RequestHeadersJson { get; set; }

    /// <summary>
    /// Request body content.
    /// </summary>
    [Column("RequestBody")]
    public string? RequestBody { get; set; }

    /// <summary>
    /// Request body size in bytes.
    /// </summary>
    [Column("RequestSize")]
    public long RequestSize { get; set; }

    // Response properties

    /// <summary>
    /// HTTP status code (e.g., 200, 404, 500).
    /// </summary>
    [Indexed]
    [Column("StatusCode")]
    public int StatusCode { get; set; }

    /// <summary>
    /// HTTP reason phrase (e.g., "OK", "Not Found").
    /// </summary>
    [Column("ReasonPhrase")]
    public string? ReasonPhrase { get; set; }

    /// <summary>
    /// Response headers serialized as JSON.
    /// </summary>
    [Column("ResponseHeadersJson")]
    public string? ResponseHeadersJson { get; set; }

    /// <summary>
    /// Response body content.
    /// </summary>
    [Column("ResponseBody")]
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Response body size in bytes.
    /// </summary>
    [Column("ResponseSize")]
    public long ResponseSize { get; set; }

    // State properties

    /// <summary>
    /// Whether the request/response cycle completed successfully.
    /// </summary>
    [Column("IsComplete")]
    public bool IsComplete { get; set; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the request body was truncated.
    /// </summary>
    [Column("RequestBodyTruncated")]
    public bool RequestBodyTruncated { get; set; }

    /// <summary>
    /// Whether the response body was truncated.
    /// </summary>
    [Column("ResponseBodyTruncated")]
    public bool ResponseBodyTruncated { get; set; }

    /// <summary>
    /// Converts an HttpRecord to an entity for database storage.
    /// </summary>
    public static HttpRecordEntity FromRecord(HttpRecord record)
    {
        return new HttpRecordEntity
        {
            Id = record.Id.ToString(),
            Timestamp = record.Timestamp,
            DurationMs = (long)record.Duration.TotalMilliseconds,
            Method = record.Method,
            Url = record.Url,
            RequestHeadersJson = SerializeHeaders(record.RequestHeaders),
            RequestBody = record.RequestBody,
            RequestSize = record.RequestSize,
            StatusCode = record.StatusCode,
            ReasonPhrase = record.ReasonPhrase,
            ResponseHeadersJson = SerializeHeaders(record.ResponseHeaders),
            ResponseBody = record.ResponseBody,
            ResponseSize = record.ResponseSize,
            IsComplete = record.IsComplete,
            ErrorMessage = record.ErrorMessage,
            RequestBodyTruncated = record.RequestBodyTruncated,
            ResponseBodyTruncated = record.ResponseBodyTruncated
        };
    }

    /// <summary>
    /// Converts this entity back to an HttpRecord.
    /// </summary>
    public HttpRecord ToRecord()
    {
        return new HttpRecord
        {
            Id = Guid.Parse(Id),
            Timestamp = Timestamp,
            Duration = TimeSpan.FromMilliseconds(DurationMs),
            Method = Method,
            Url = Url,
            RequestHeaders = DeserializeHeaders(RequestHeadersJson),
            RequestBody = RequestBody,
            RequestSize = RequestSize,
            StatusCode = StatusCode,
            ReasonPhrase = ReasonPhrase,
            ResponseHeaders = DeserializeHeaders(ResponseHeadersJson),
            ResponseBody = ResponseBody,
            ResponseSize = ResponseSize,
            IsComplete = IsComplete,
            ErrorMessage = ErrorMessage,
            RequestBodyTruncated = RequestBodyTruncated,
            ResponseBodyTruncated = ResponseBodyTruncated
        };
    }

    private static string? SerializeHeaders(Dictionary<string, string[]>? headers)
    {
        if (headers == null || headers.Count == 0)
            return null;

        return JsonSerializer.Serialize(headers);
    }

    private static Dictionary<string, string[]> DeserializeHeaders(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, string[]>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)
                   ?? new Dictionary<string, string[]>();
        }
        catch
        {
            return new Dictionary<string, string[]>();
        }
    }
}
