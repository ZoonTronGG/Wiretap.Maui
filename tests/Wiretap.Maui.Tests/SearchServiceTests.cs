using Xunit;
using Wiretap.Maui.Core;
using Wiretap.Maui.Services;

namespace Wiretap.Maui.Tests;

public class SearchServiceTests
{
    private readonly SearchService _service = new();

    private static HttpRecord CreateRecord(
        string method = "GET",
        string url = "https://api.example.com/users",
        int statusCode = 200,
        bool isComplete = true,
        string? requestBody = null,
        string? responseBody = null,
        Dictionary<string, string[]>? requestHeaders = null,
        Dictionary<string, string[]>? responseHeaders = null)
    {
        return new HttpRecord
        {
            Method = method,
            Url = url,
            StatusCode = statusCode,
            IsComplete = isComplete,
            RequestBody = requestBody,
            ResponseBody = responseBody,
            RequestHeaders = requestHeaders ?? new Dictionary<string, string[]>(),
            ResponseHeaders = responseHeaders ?? new Dictionary<string, string[]>()
        };
    }

    #region Empty Filter Tests

    [Fact]
    public void Filter_EmptyFilter_ReturnsAllRecords()
    {
        var records = new[]
        {
            CreateRecord("GET", "https://api.example.com/users", 200),
            CreateRecord("POST", "https://api.example.com/orders", 201),
            CreateRecord("DELETE", "https://api.example.com/items/1", 500)
        };

        var result = _service.Filter(records, RecordFilter.Empty).ToList();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_NullRecords_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.Filter(null!, RecordFilter.Empty).ToList());
    }

    [Fact]
    public void Filter_NullFilter_ThrowsArgumentNullException()
    {
        var records = new[] { CreateRecord() };
        Assert.Throws<ArgumentNullException>(() =>
            _service.Filter(records, null!).ToList());
    }

    #endregion

    #region Search Text Tests

    [Fact]
    public void Filter_SearchText_MatchesUrl()
    {
        var records = new[]
        {
            CreateRecord(url: "https://api.example.com/users"),
            CreateRecord(url: "https://api.example.com/orders"),
            CreateRecord(url: "https://api.test.com/products")
        };

        var filter = RecordFilter.WithSearchText("example");
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Contains("example", r.Url));
    }

    [Fact]
    public void Filter_SearchText_CaseInsensitive()
    {
        var records = new[]
        {
            CreateRecord(url: "https://API.EXAMPLE.COM/USERS"),
            CreateRecord(url: "https://api.example.com/users"),
            CreateRecord(url: "https://Api.Example.Com/Users")
        };

        var filter = RecordFilter.WithSearchText("example");
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_SearchText_MatchesRequestBody()
    {
        var records = new[]
        {
            CreateRecord(requestBody: "{\"name\":\"John\"}"),
            CreateRecord(requestBody: "{\"name\":\"Jane\"}"),
            CreateRecord(requestBody: "{\"email\":\"test@test.com\"}")
        };

        var filter = RecordFilter.WithSearchText("John");
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
        Assert.Contains("John", result[0].RequestBody);
    }

    [Fact]
    public void Filter_SearchText_MatchesResponseBody()
    {
        var records = new[]
        {
            CreateRecord(responseBody: "{\"success\":true}"),
            CreateRecord(responseBody: "{\"error\":\"not found\"}"),
            CreateRecord(responseBody: "{\"data\":[1,2,3]}")
        };

        var filter = RecordFilter.WithSearchText("error");
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
        Assert.Contains("error", result[0].ResponseBody);
    }

    [Fact]
    public void Filter_SearchText_MatchesRequestHeaders()
    {
        var records = new[]
        {
            CreateRecord(requestHeaders: new Dictionary<string, string[]>
            {
                { "Authorization", new[] { "Bearer token123" } }
            }),
            CreateRecord(requestHeaders: new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } }
            })
        };

        var filter = RecordFilter.WithSearchText("Bearer");
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void Filter_SearchText_MatchesHeaderName()
    {
        var records = new[]
        {
            CreateRecord(requestHeaders: new Dictionary<string, string[]>
            {
                { "X-Custom-Header", new[] { "value" } }
            }),
            CreateRecord(requestHeaders: new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "text/plain" } }
            })
        };

        var filter = RecordFilter.WithSearchText("Custom");
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void Filter_SearchText_MatchesResponseHeaders()
    {
        var records = new[]
        {
            CreateRecord(responseHeaders: new Dictionary<string, string[]>
            {
                { "X-Request-Id", new[] { "abc-123-xyz" } }
            }),
            CreateRecord(responseHeaders: new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } }
            })
        };

        var filter = RecordFilter.WithSearchText("abc-123");
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void Filter_SearchText_WhitespaceOnly_ReturnsAllRecords()
    {
        var records = new[]
        {
            CreateRecord(url: "https://api.example.com/users"),
            CreateRecord(url: "https://api.example.com/orders")
        };

        var filter = new RecordFilter { SearchText = "   " };
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_SearchText_NoMatch_ReturnsEmpty()
    {
        var records = new[]
        {
            CreateRecord(url: "https://api.example.com/users"),
            CreateRecord(url: "https://api.example.com/orders")
        };

        var filter = RecordFilter.WithSearchText("nonexistent");
        var result = _service.Filter(records, filter).ToList();

        Assert.Empty(result);
    }

    #endregion

    #region Method Filter Tests

    [Fact]
    public void Filter_SingleMethod_MatchesMethod()
    {
        var records = new[]
        {
            CreateRecord("GET"),
            CreateRecord("POST"),
            CreateRecord("PUT"),
            CreateRecord("DELETE")
        };

        var filter = RecordFilter.WithMethods("GET");
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
        Assert.Equal("GET", result[0].Method);
    }

    [Fact]
    public void Filter_MultipleMethods_MatchesAnyMethod()
    {
        var records = new[]
        {
            CreateRecord("GET"),
            CreateRecord("POST"),
            CreateRecord("PUT"),
            CreateRecord("DELETE")
        };

        var filter = RecordFilter.WithMethods("GET", "POST");
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Method == "GET");
        Assert.Contains(result, r => r.Method == "POST");
    }

    [Fact]
    public void Filter_Method_CaseInsensitive()
    {
        var records = new[]
        {
            CreateRecord("GET"),
            CreateRecord("get"),
            CreateRecord("Get")
        };

        var filter = RecordFilter.WithMethods("GET");
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Filter_EmptyMethods_ReturnsAllRecords()
    {
        var records = new[]
        {
            CreateRecord("GET"),
            CreateRecord("POST"),
            CreateRecord("DELETE")
        };

        var filter = new RecordFilter { Methods = new HashSet<string>() };
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(3, result.Count);
    }

    [Theory]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Filter_LesserUsedMethods_MatchCorrectly(string method)
    {
        var records = new[]
        {
            CreateRecord("GET"),
            CreateRecord(method),
            CreateRecord("POST")
        };

        var filter = RecordFilter.WithMethods(method);
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
        Assert.Equal(method, result[0].Method);
    }

    #endregion

    #region Status Group Filter Tests

    [Theory]
    [InlineData(200, 2)]
    [InlineData(201, 2)]
    [InlineData(204, 2)]
    [InlineData(301, 3)]
    [InlineData(302, 3)]
    [InlineData(400, 4)]
    [InlineData(401, 4)]
    [InlineData(404, 4)]
    [InlineData(500, 5)]
    [InlineData(502, 5)]
    [InlineData(503, 5)]
    public void Filter_StatusGroup_MatchesStatusCode(int statusCode, int expectedGroup)
    {
        var records = new[]
        {
            CreateRecord(statusCode: statusCode)
        };

        var filter = RecordFilter.WithStatusGroups(expectedGroup);
        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void Filter_StatusGroup_2xx_MatchesAllSuccess()
    {
        var records = new[]
        {
            CreateRecord(statusCode: 200),
            CreateRecord(statusCode: 201),
            CreateRecord(statusCode: 204),
            CreateRecord(statusCode: 400),
            CreateRecord(statusCode: 500)
        };

        var filter = RecordFilter.WithStatusGroups(2);
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.True(r.StatusCode >= 200 && r.StatusCode < 300));
    }

    [Fact]
    public void Filter_StatusGroup_4xxAnd5xx_MatchesAllErrors()
    {
        var records = new[]
        {
            CreateRecord(statusCode: 200),
            CreateRecord(statusCode: 201),
            CreateRecord(statusCode: 400),
            CreateRecord(statusCode: 404),
            CreateRecord(statusCode: 500),
            CreateRecord(statusCode: 503)
        };

        var filter = RecordFilter.WithStatusGroups(4, 5);
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(4, result.Count);
        Assert.All(result, r => Assert.True(r.StatusCode >= 400));
    }

    [Fact]
    public void Filter_StatusGroup_0_MatchesFailedRequests()
    {
        var records = new[]
        {
            CreateRecord(statusCode: 200, isComplete: true),
            CreateRecord(statusCode: 0, isComplete: false),
            CreateRecord(statusCode: 0, isComplete: false)
        };

        var filter = RecordFilter.WithStatusGroups(0);
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.False(r.IsComplete));
    }

    [Fact]
    public void Filter_StatusGroup_EmptySet_ReturnsAllRecords()
    {
        var records = new[]
        {
            CreateRecord(statusCode: 200),
            CreateRecord(statusCode: 404),
            CreateRecord(statusCode: 500)
        };

        var filter = new RecordFilter { StatusGroups = new HashSet<int>() };
        var result = _service.Filter(records, filter).ToList();

        Assert.Equal(3, result.Count);
    }

    #endregion

    #region Combined Filter Tests

    [Fact]
    public void Filter_SearchTextAndMethod_CombinesWithAnd()
    {
        var records = new[]
        {
            CreateRecord("GET", "https://api.example.com/users"),
            CreateRecord("POST", "https://api.example.com/users"),
            CreateRecord("GET", "https://api.example.com/orders"),
            CreateRecord("POST", "https://api.example.com/orders")
        };

        var filter = new RecordFilter
        {
            SearchText = "users",
            Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET" }
        };

        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
        Assert.Equal("GET", result[0].Method);
        Assert.Contains("users", result[0].Url);
    }

    [Fact]
    public void Filter_AllCriteria_CombinesWithAnd()
    {
        var records = new[]
        {
            CreateRecord("GET", "https://api.example.com/users", 200),
            CreateRecord("GET", "https://api.example.com/users", 404),
            CreateRecord("POST", "https://api.example.com/users", 200),
            CreateRecord("GET", "https://api.example.com/orders", 200)
        };

        var filter = new RecordFilter
        {
            SearchText = "users",
            Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET" },
            StatusGroups = new HashSet<int> { 2 }
        };

        var result = _service.Filter(records, filter).ToList();

        Assert.Single(result);
        Assert.Equal("GET", result[0].Method);
        Assert.Equal(200, result[0].StatusCode);
        Assert.Contains("users", result[0].Url);
    }

    [Fact]
    public void Filter_AllCriteria_NoMatch_ReturnsEmpty()
    {
        var records = new[]
        {
            CreateRecord("GET", "https://api.example.com/users", 200),
            CreateRecord("POST", "https://api.example.com/orders", 404)
        };

        var filter = new RecordFilter
        {
            SearchText = "products",
            Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DELETE" },
            StatusGroups = new HashSet<int> { 5 }
        };

        var result = _service.Filter(records, filter).ToList();

        Assert.Empty(result);
    }

    #endregion

    #region Matches Method Tests

    [Fact]
    public void Matches_NullRecord_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.Matches(null!, RecordFilter.Empty));
    }

    [Fact]
    public void Matches_NullFilter_ThrowsArgumentNullException()
    {
        var record = CreateRecord();
        Assert.Throws<ArgumentNullException>(() =>
            _service.Matches(record, null!));
    }

    [Fact]
    public void Matches_EmptyFilter_ReturnsTrue()
    {
        var record = CreateRecord();
        Assert.True(_service.Matches(record, RecordFilter.Empty));
    }

    [Fact]
    public void Matches_MatchingCriteria_ReturnsTrue()
    {
        var record = CreateRecord("POST", "https://api.example.com/users", 201);
        var filter = new RecordFilter
        {
            SearchText = "users",
            Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "POST" },
            StatusGroups = new HashSet<int> { 2 }
        };

        Assert.True(_service.Matches(record, filter));
    }

    [Fact]
    public void Matches_NonMatchingCriteria_ReturnsFalse()
    {
        var record = CreateRecord("GET", "https://api.example.com/users", 200);
        var filter = RecordFilter.WithMethods("POST");

        Assert.False(_service.Matches(record, filter));
    }

    #endregion

    #region RecordFilter Builder Tests

    [Fact]
    public void RecordFilter_AddMethod_AddsToSet()
    {
        var filter = new RecordFilter()
            .AddMethod("GET")
            .AddMethod("POST");

        Assert.Equal(2, filter.Methods.Count);
        Assert.Contains("GET", filter.Methods);
        Assert.Contains("POST", filter.Methods);
    }

    [Fact]
    public void RecordFilter_RemoveMethod_RemovesFromSet()
    {
        var filter = RecordFilter.WithMethods("GET", "POST", "DELETE")
            .RemoveMethod("POST");

        Assert.Equal(2, filter.Methods.Count);
        Assert.DoesNotContain("POST", filter.Methods);
    }

    [Fact]
    public void RecordFilter_AddStatusGroup_AddsToSet()
    {
        var filter = new RecordFilter()
            .AddStatusGroup(2)
            .AddStatusGroup(4);

        Assert.Equal(2, filter.StatusGroups.Count);
        Assert.Contains(2, filter.StatusGroups);
        Assert.Contains(4, filter.StatusGroups);
    }

    [Fact]
    public void RecordFilter_RemoveStatusGroup_RemovesFromSet()
    {
        var filter = RecordFilter.WithStatusGroups(2, 4, 5)
            .RemoveStatusGroup(4);

        Assert.Equal(2, filter.StatusGroups.Count);
        Assert.DoesNotContain(4, filter.StatusGroups);
    }

    [Fact]
    public void RecordFilter_Clear_ResetsAllCriteria()
    {
        var filter = new RecordFilter
        {
            SearchText = "test",
            Methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET" },
            StatusGroups = new HashSet<int> { 2 }
        };

        filter.Clear();

        Assert.True(filter.IsEmpty);
        Assert.Null(filter.SearchText);
        Assert.Empty(filter.Methods);
        Assert.Empty(filter.StatusGroups);
    }

    [Fact]
    public void RecordFilter_IsEmpty_TrueWhenNoCriteria()
    {
        var filter = new RecordFilter();
        Assert.True(filter.IsEmpty);
    }

    [Fact]
    public void RecordFilter_IsEmpty_FalseWithSearchText()
    {
        var filter = new RecordFilter { SearchText = "test" };
        Assert.False(filter.IsEmpty);
    }

    [Fact]
    public void RecordFilter_IsEmpty_FalseWithMethods()
    {
        var filter = RecordFilter.WithMethods("GET");
        Assert.False(filter.IsEmpty);
    }

    [Fact]
    public void RecordFilter_IsEmpty_FalseWithStatusGroups()
    {
        var filter = RecordFilter.WithStatusGroups(2);
        Assert.False(filter.IsEmpty);
    }

    #endregion
}
