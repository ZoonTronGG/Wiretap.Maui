using Xunit;
using Wiretap.Maui.Core;
using Wiretap.Maui.Services;

namespace Wiretap.Maui.Tests;

public class PdfExporterTests
{
    private static HttpRecord CreateTestRecord(
        string method = "GET",
        string url = "https://api.example.com/users",
        int statusCode = 200,
        string? requestBody = null,
        string? responseBody = null)
    {
        return new HttpRecord
        {
            Method = method,
            Url = url,
            StatusCode = statusCode,
            ReasonPhrase = GetReasonPhrase(statusCode),
            IsComplete = true,
            Duration = TimeSpan.FromMilliseconds(150),
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } },
                { "Accept", new[] { "application/json" } },
                { "Authorization", new[] { "Bearer token123" } }
            },
            RequestBody = requestBody,
            RequestSize = requestBody?.Length ?? 0,
            ResponseHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } },
                { "X-Request-Id", new[] { "abc-123-def" } }
            },
            ResponseBody = responseBody,
            ResponseSize = responseBody?.Length ?? 0
        };
    }

    private static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        201 => "Created",
        400 => "Bad Request",
        401 => "Unauthorized",
        404 => "Not Found",
        500 => "Internal Server Error",
        _ => ""
    };

    #region ToPdf Tests

    [Fact]
    public void ToPdf_GeneratesValidPdfBytes()
    {
        var record = CreateTestRecord();

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        // PDF files start with %PDF
        Assert.Equal(0x25, pdfBytes[0]); // %
        Assert.Equal(0x50, pdfBytes[1]); // P
        Assert.Equal(0x44, pdfBytes[2]); // D
        Assert.Equal(0x46, pdfBytes[3]); // F
    }

    [Fact]
    public void ToPdf_HandlesGetRequest()
    {
        var record = CreateTestRecord(method: "GET", responseBody: "{\"users\": []}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesPostRequest()
    {
        var record = CreateTestRecord(
            method: "POST",
            requestBody: "{\"name\": \"John\", \"email\": \"john@example.com\"}",
            responseBody: "{\"id\": 123, \"created\": true}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesPutRequest()
    {
        var record = CreateTestRecord(
            method: "PUT",
            url: "https://api.example.com/users/123",
            requestBody: "{\"name\": \"Updated Name\"}",
            responseBody: "{\"updated\": true}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesDeleteRequest()
    {
        var record = CreateTestRecord(
            method: "DELETE",
            url: "https://api.example.com/users/123",
            statusCode: 204);

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesEmptyBodies()
    {
        var record = CreateTestRecord(requestBody: null, responseBody: null);

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesErrorResponse()
    {
        var record = CreateTestRecord(
            statusCode: 500,
            responseBody: "{\"error\": \"Internal server error\"}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesClientError()
    {
        var record = CreateTestRecord(
            statusCode: 404,
            responseBody: "{\"error\": \"User not found\"}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesFailedRequest()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/timeout",
            IsComplete = false,
            ErrorMessage = "The operation was canceled due to timeout",
            RequestHeaders = new Dictionary<string, string[]>(),
            ResponseHeaders = new Dictionary<string, string[]>()
        };

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesLargeBody()
    {
        var largeBody = new string('x', 15000); // 15KB of text
        var record = CreateTestRecord(responseBody: largeBody);

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesJsonBody()
    {
        var jsonBody = @"{
    ""users"": [
        {""id"": 1, ""name"": ""Alice"", ""email"": ""alice@example.com""},
        {""id"": 2, ""name"": ""Bob"", ""email"": ""bob@example.com""}
    ],
    ""total"": 2,
    ""page"": 1
}";
        var record = CreateTestRecord(responseBody: jsonBody);

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesMultiValueHeaders()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/test",
            StatusCode = 200,
            IsComplete = true,
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Accept", new[] { "application/json", "text/plain", "*/*" } }
            },
            ResponseHeaders = new Dictionary<string, string[]>
            {
                { "Set-Cookie", new[] { "session=abc123", "theme=dark" } }
            }
        };

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesTruncatedBodies()
    {
        var record = CreateTestRecord(
            requestBody: "truncated request",
            responseBody: "truncated response");
        record.RequestBodyTruncated = true;
        record.ResponseBodyTruncated = true;

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_ThrowsOnNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => PdfExporter.ToPdf(null!));
    }

    #endregion

    #region ToPdfFile Tests

    [Fact]
    public void ToPdfFile_CreatesFile()
    {
        var record = CreateTestRecord(responseBody: "{\"test\": true}");
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");

        try
        {
            PdfExporter.ToPdfFile(record, tempFile);

            Assert.True(File.Exists(tempFile));
            var fileContent = File.ReadAllBytes(tempFile);
            Assert.NotEmpty(fileContent);
            // PDF signature
            Assert.Equal(0x25, fileContent[0]);
            Assert.Equal(0x50, fileContent[1]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region ToPdfStream Tests

    [Fact]
    public void ToPdfStream_WritesToStream()
    {
        var record = CreateTestRecord(responseBody: "{\"test\": true}");
        using var stream = new MemoryStream();

        PdfExporter.ToPdfStream(record, stream);

        Assert.True(stream.Length > 0);
        stream.Position = 0;
        Assert.Equal(0x25, stream.ReadByte()); // %
        Assert.Equal(0x50, stream.ReadByte()); // P
    }

    #endregion

    #region GenerateFileName Tests

    [Fact]
    public void GenerateFileName_IncludesMethod()
    {
        var record = CreateTestRecord(method: "POST");

        var fileName = PdfExporter.GenerateFileName(record);

        Assert.Contains("POST", fileName);
    }

    [Fact]
    public void GenerateFileName_IncludesTimestamp()
    {
        var record = CreateTestRecord();

        var fileName = PdfExporter.GenerateFileName(record);

        Assert.Matches(@"wiretap_GET_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.pdf", fileName);
    }

    [Fact]
    public void GenerateFileName_HasPdfExtension()
    {
        var record = CreateTestRecord();

        var fileName = PdfExporter.GenerateFileName(record);

        Assert.EndsWith(".pdf", fileName);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void GenerateFileName_WorksForAllMethods(string method)
    {
        var record = CreateTestRecord(method: method);

        var fileName = PdfExporter.GenerateFileName(record);

        Assert.Contains(method.ToUpperInvariant(), fileName);
        Assert.EndsWith(".pdf", fileName);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToPdf_HandlesEmptyHeaders()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/test",
            StatusCode = 200,
            IsComplete = true,
            RequestHeaders = new Dictionary<string, string[]>(),
            ResponseHeaders = new Dictionary<string, string[]>()
        };

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesSpecialCharactersInUrl()
    {
        var record = CreateTestRecord(
            url: "https://api.example.com/search?q=hello%20world&filter=name%3D%22test%22");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesSpecialCharactersInBody()
    {
        var record = CreateTestRecord(
            requestBody: "{\"message\": \"Hello <World> & 'Friends'\"}",
            responseBody: "{\"result\": \"Success with special chars: <>&'\\\"\"}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesUnicodeInBody()
    {
        var record = CreateTestRecord(
            responseBody: "{\"greeting\": \"Hello World\", \"emoji\": \"\\ud83d\\ude00\"}");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ToPdf_HandlesNewlinesInBody()
    {
        var record = CreateTestRecord(
            responseBody: "Line 1\nLine 2\nLine 3\r\nLine 4");

        var pdfBytes = PdfExporter.ToPdf(record);

        Assert.NotEmpty(pdfBytes);
    }

    #endregion
}
