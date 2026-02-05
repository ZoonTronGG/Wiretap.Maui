using Xunit;
using Wiretap.Maui.Core;
using Wiretap.Maui.Services;

namespace Wiretap.Maui.Tests;

public class CurlExporterTests
{
    [Fact]
    public void ToCurl_SimpleGetRequest_GeneratesValidCommand()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/users"
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.StartsWith("curl", curl);
        Assert.Contains("'https://api.example.com/users'", curl);
        // GET is default, so -X GET should not be present
        Assert.DoesNotContain("-X GET", curl);
    }

    [Fact]
    public void ToCurl_PostRequest_IncludesMethod()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users"
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains("-X POST", curl);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void ToCurl_NonGetMethods_IncludesMethodFlag(string method)
    {
        var record = new HttpRecord
        {
            Method = method,
            Url = "https://api.example.com/resource"
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains($"-X {method}", curl);
    }

    [Fact]
    public void ToCurl_WithHeaders_IncludesAllHeaders()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } },
                { "Authorization", new[] { "Bearer token123" } }
            }
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains("-H 'Content-Type: application/json'", curl);
        Assert.Contains("-H 'Authorization: Bearer token123'", curl);
    }

    [Fact]
    public void ToCurl_WithMultiValueHeader_IncludesEachValue()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Accept", new[] { "application/json", "text/plain" } }
            }
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains("-H 'Accept: application/json'", curl);
        Assert.Contains("-H 'Accept: text/plain'", curl);
    }

    [Fact]
    public void ToCurl_WithBody_IncludesDataFlag()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestBody = "{\"name\":\"John\"}"
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains("-d '{\"name\":\"John\"}'", curl);
    }

    [Fact]
    public void ToCurl_SkipsContentLengthHeader()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Length", new[] { "42" } },
                { "Content-Type", new[] { "application/json" } }
            }
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.DoesNotContain("Content-Length", curl);
        Assert.Contains("Content-Type", curl);
    }

    [Fact]
    public void ToCurl_SkipsHostHeader()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Host", new[] { "api.example.com" } },
                { "Accept", new[] { "application/json" } }
            }
        };

        var curl = CurlExporter.ToCurl(record);

        // Host header should be skipped (curl derives it from URL)
        Assert.DoesNotContain("-H 'Host:", curl);
        Assert.Contains("-H 'Accept:", curl);
    }

    [Fact]
    public void ToCurl_EscapesSingleQuotes()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/search",
            RequestBody = "{ \"query\": \"it's a test\" }"
        };

        var curl = CurlExporter.ToCurl(record);

        // Single quote should be escaped as '\''
        Assert.Contains("it'\\''s a test", curl);
    }

    [Fact]
    public void ToCurl_EscapesSingleQuotesInHeaders()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "X-Custom", new[] { "value's with quote" } }
            }
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains("value'\\''s with quote", curl);
    }

    [Fact]
    public void ToCurl_EscapesSingleQuotesInUrl()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/search?q=it's"
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.Contains("it'\\''s", curl);
    }

    [Fact]
    public void ToCurl_CompleteRequest_HasCorrectStructure()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } },
                { "Authorization", new[] { "Bearer token" } }
            },
            RequestBody = "{\"name\":\"John\"}"
        };

        var curl = CurlExporter.ToCurl(record);

        // Verify structure: curl -X POST -H ... -H ... -d ... URL
        Assert.StartsWith("curl", curl);
        Assert.Contains("-X POST", curl);
        Assert.Contains("-H 'Content-Type: application/json'", curl);
        Assert.Contains("-H 'Authorization: Bearer token'", curl);
        Assert.Contains("-d '{\"name\":\"John\"}'", curl);
        Assert.EndsWith("'https://api.example.com/users'", curl);
    }

    [Fact]
    public void ToCurl_NullRecord_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CurlExporter.ToCurl(null!));
    }

    [Fact]
    public void ToCurlCompact_GeneratesSingleLine()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } }
            },
            RequestBody = "{\"name\":\"John\"}"
        };

        var curl = CurlExporter.ToCurlCompact(record);

        // Compact version should not have line continuations
        Assert.DoesNotContain("\\\n", curl);
        Assert.DoesNotContain("\n", curl);
    }

    [Fact]
    public void ToCurl_MultilineFormat_HasLineContinuations()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } }
            },
            RequestBody = "{\"name\":\"John\"}"
        };

        var curl = CurlExporter.ToCurl(record);

        // Regular version should have line continuations for readability
        Assert.Contains(" \\\n", curl);
    }

    [Fact]
    public void ToCurl_EmptyBody_DoesNotIncludeDataFlag()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestBody = null
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.DoesNotContain("-d", curl);
    }

    [Fact]
    public void ToCurl_EmptyStringBody_DoesNotIncludeDataFlag()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/users",
            RequestBody = ""
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.DoesNotContain("-d", curl);
    }

    [Fact]
    public void ToCurl_NoHeaders_StillGeneratesValidCommand()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/users"
        };

        var curl = CurlExporter.ToCurl(record);

        Assert.DoesNotContain("-H", curl);
        Assert.Contains("curl", curl);
        Assert.Contains("https://api.example.com/users", curl);
    }
}
