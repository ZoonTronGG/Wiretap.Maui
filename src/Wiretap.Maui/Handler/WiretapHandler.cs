using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Wiretap.Maui.Core;

namespace Wiretap.Maui.Handler;

/// <summary>
/// HTTP message handler that intercepts and records all HTTP traffic for inspection.
/// Clones request/response bodies without consuming the original streams.
/// </summary>
public class WiretapHandler : DelegatingHandler
{
    private readonly IWiretapStore _store;
    private readonly WiretapOptions _options;

    /// <summary>
    /// Creates a new WiretapHandler.
    /// </summary>
    /// <param name="store">The store for captured HTTP records.</param>
    /// <param name="options">Configuration options.</param>
    public WiretapHandler(IWiretapStore store, WiretapOptions options)
    {
        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var record = new HttpRecord
        {
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString() ?? string.Empty
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Capture request details
            await CaptureRequestAsync(request, record, cancellationToken);

            // Send the request
            var response = await base.SendAsync(request, cancellationToken);

            stopwatch.Stop();
            record.Duration = stopwatch.Elapsed;

            // Capture response details
            await CaptureResponseAsync(response, record, cancellationToken);

            record.IsComplete = true;
            _store.Add(record);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            record.Duration = stopwatch.Elapsed;
            record.IsComplete = false;
            record.ErrorMessage = ex.Message;
            _store.Add(record);

            throw;
        }
    }

    private async Task CaptureRequestAsync(
        HttpRequestMessage request,
        HttpRecord record,
        CancellationToken cancellationToken)
    {
        // Capture original content headers BEFORE reading content
        Dictionary<string, IEnumerable<string>>? originalContentHeaders = null;
        if (request.Content != null)
        {
            originalContentHeaders = request.Content.Headers.ToDictionary(h => h.Key, h => h.Value);
        }

        // Capture headers for display
        if (_options.CaptureRequestHeaders)
        {
            record.RequestHeaders.Clear();
            CaptureHeaders(request.Headers, record.RequestHeaders);

            if (request.Content != null)
            {
                CaptureHeaders(request.Content.Headers, record.RequestHeaders);
            }
        }

        // Capture body
        if (request.Content != null)
        {
            var (body, size, truncated) = await CloneContentAsync(
                request.Content,
                _options.MaxBodySizeBytes,
                cancellationToken);

            record.RequestBody = body;
            record.RequestSize = size;
            record.RequestBodyTruncated = truncated;

            // Recreate content to preserve the stream for downstream handlers
            if (!string.IsNullOrEmpty(body))
            {
                var mediaType = request.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var encoding = GetEncoding(request.Content.Headers.ContentType);

                request.Content = new StringContent(body, encoding, mediaType);

                // Copy back ONLY the original content headers (not request headers)
                if (originalContentHeaders != null)
                {
                    foreach (var header in originalContentHeaders)
                    {
                        // Skip Content-Type as it's already set by StringContent
                        if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!request.Content.Headers.Contains(header.Key))
                        {
                            try
                            {
                                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }
                            catch
                            {
                                // Ignore headers that can't be added
                            }
                        }
                    }
                }
            }
        }
    }

    private async Task CaptureResponseAsync(
        HttpResponseMessage response,
        HttpRecord record,
        CancellationToken cancellationToken)
    {
        record.StatusCode = (int)response.StatusCode;
        record.ReasonPhrase = response.ReasonPhrase;

        // Capture headers
        if (_options.CaptureResponseHeaders)
        {
            record.ResponseHeaders.Clear();
            CaptureHeaders(response.Headers, record.ResponseHeaders);

            if (response.Content != null)
            {
                CaptureHeaders(response.Content.Headers, record.ResponseHeaders);
            }
        }

        // Capture body
        if (response.Content != null)
        {
            var (body, size, truncated) = await CloneContentAsync(
                response.Content,
                _options.MaxBodySizeBytes,
                cancellationToken);

            record.ResponseBody = body;
            record.ResponseSize = size;
            record.ResponseBodyTruncated = truncated;

            // Recreate content to preserve the stream for the caller
            if (body != null)
            {
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var encoding = GetEncoding(response.Content.Headers.ContentType);

                var newContent = new StringContent(body, encoding, mediaType);

                // Copy back content headers
                foreach (var header in response.Content.Headers)
                {
                    if (!newContent.Headers.Contains(header.Key))
                    {
                        try
                        {
                            newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                        catch
                        {
                            // Ignore headers that can't be added
                        }
                    }
                }

                response.Content = newContent;
            }
        }
    }

    private void CaptureHeaders(HttpHeaders headers, Dictionary<string, string[]> target)
    {
        foreach (var header in headers)
        {
            var values = header.Value.ToArray();

            // Mask sensitive headers if enabled
            if (_options.MaskSensitiveHeaders && IsSensitiveHeader(header.Key))
            {
                values = values.Select(_ => "[MASKED]").ToArray();
            }

            target[header.Key] = values;
        }
    }

    private bool IsSensitiveHeader(string headerName)
    {
        return _options.SensitiveHeaderPatterns.Any(pattern =>
            headerName.Equals(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(string? Body, long Size, bool Truncated)> CloneContentAsync(
        HttpContent content,
        int maxSize,
        CancellationToken cancellationToken)
    {
        try
        {
            // Read the content as bytes
            var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
            var size = bytes.Length;
            var truncated = false;

            // Truncate if too large
            if (bytes.Length > maxSize)
            {
                bytes = bytes[..maxSize];
                truncated = true;
            }

            // Try to decode as string
            var encoding = GetEncoding(content.Headers.ContentType);
            var body = encoding.GetString(bytes);

            return (body, size, truncated);
        }
        catch
        {
            // If we can't read the content, return empty
            return (null, 0, false);
        }
    }

    private static Encoding GetEncoding(MediaTypeHeaderValue? contentType)
    {
        if (contentType?.CharSet != null)
        {
            try
            {
                return Encoding.GetEncoding(contentType.CharSet);
            }
            catch
            {
                // Fall through to default
            }
        }

        return Encoding.UTF8;
    }
}
