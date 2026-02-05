using Xunit;
using Wiretap.Maui.Core;

namespace Wiretap.Maui.Tests;

public class HttpRecordTests
{
    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(199, false)]
    [InlineData(300, false)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    public void IsSuccess_ReturnsCorrectValue(int statusCode, bool expected)
    {
        var record = new HttpRecord { StatusCode = statusCode };
        Assert.Equal(expected, record.IsSuccess);
    }

    [Theory]
    [InlineData(300, true)]
    [InlineData(301, true)]
    [InlineData(302, true)]
    [InlineData(399, true)]
    [InlineData(200, false)]
    [InlineData(400, false)]
    public void IsRedirect_ReturnsCorrectValue(int statusCode, bool expected)
    {
        var record = new HttpRecord { StatusCode = statusCode };
        Assert.Equal(expected, record.IsRedirect);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(401, true)]
    [InlineData(404, true)]
    [InlineData(499, true)]
    [InlineData(200, false)]
    [InlineData(500, false)]
    public void IsClientError_ReturnsCorrectValue(int statusCode, bool expected)
    {
        var record = new HttpRecord { StatusCode = statusCode };
        Assert.Equal(expected, record.IsClientError);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(503, true)]
    [InlineData(599, true)]
    [InlineData(200, false)]
    [InlineData(400, false)]
    public void IsServerError_ReturnsCorrectValue(int statusCode, bool expected)
    {
        var record = new HttpRecord { StatusCode = statusCode };
        Assert.Equal(expected, record.IsServerError);
    }

    [Fact]
    public void IsFailed_ReturnsTrueWhenNotCompleteWithError()
    {
        var record = new HttpRecord
        {
            IsComplete = false,
            ErrorMessage = "Connection refused"
        };

        Assert.True(record.IsFailed);
    }

    [Fact]
    public void IsFailed_ReturnsFalseWhenComplete()
    {
        var record = new HttpRecord
        {
            IsComplete = true,
            StatusCode = 200
        };

        Assert.False(record.IsFailed);
    }

    [Fact]
    public void IsFailed_ReturnsFalseWhenNoErrorMessage()
    {
        var record = new HttpRecord
        {
            IsComplete = false,
            ErrorMessage = null
        };

        Assert.False(record.IsFailed);
    }

    [Fact]
    public void TotalSize_ReturnsSumOfRequestAndResponseSize()
    {
        var record = new HttpRecord
        {
            RequestSize = 100,
            ResponseSize = 500
        };

        Assert.Equal(600, record.TotalSize);
    }

    [Theory]
    [InlineData("https://api.example.com/users/123", "api.example.com/users/123")]
    [InlineData("https://example.com/api/v1/products?page=1&size=10", "example.com/api/v1/products")]
    [InlineData("", "")]
    public void DisplayUrl_FormatsCorrectly(string url, string expected)
    {
        var record = new HttpRecord { Url = url };
        Assert.Equal(expected, record.DisplayUrl);
    }

    [Fact]
    public void DisplayUrl_TruncatesLongPaths()
    {
        var record = new HttpRecord
        {
            Url = "https://example.com/api/v1/very/long/path/that/exceeds/fifty/characters/limit"
        };

        var displayUrl = record.DisplayUrl;
        // Path truncated to 50 chars (47 + "..."), plus host
        Assert.EndsWith("...", displayUrl);
        Assert.Contains("example.com", displayUrl);
        // Original path was much longer, display should be shorter
        Assert.True(displayUrl.Length < record.Url.Length);
    }

    [Theory]
    [InlineData(50, "50 ms")]
    [InlineData(999, "999 ms")]
    [InlineData(1000, "1.0 s")]
    [InlineData(1500, "1.5 s")]
    [InlineData(2345, "2.3 s")]
    public void DurationDisplay_FormatsCorrectly(int milliseconds, string expected)
    {
        var record = new HttpRecord
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds)
        };

        Assert.Equal(expected, record.DurationDisplay);
    }

    [Fact]
    public void NewRecord_HasUniqueId()
    {
        var record1 = new HttpRecord();
        var record2 = new HttpRecord();

        Assert.NotEqual(record1.Id, record2.Id);
        Assert.NotEqual(Guid.Empty, record1.Id);
    }

    [Fact]
    public void NewRecord_HasTimestamp()
    {
        var before = DateTime.UtcNow;
        var record = new HttpRecord();
        var after = DateTime.UtcNow;

        Assert.True(record.Timestamp >= before);
        Assert.True(record.Timestamp <= after);
    }

    [Fact]
    public void Headers_InitializeAsEmptyDictionaries()
    {
        var record = new HttpRecord();

        Assert.NotNull(record.RequestHeaders);
        Assert.NotNull(record.ResponseHeaders);
        Assert.Empty(record.RequestHeaders);
        Assert.Empty(record.ResponseHeaders);
    }
}
