using Xunit;
using Wiretap.Maui.Core;
using Wiretap.Maui.Database;

namespace Wiretap.Maui.Tests;

public class WiretapDatabaseTests : IAsyncLifetime
{
    private WiretapDatabase _database = null!;
    private string _databasePath = null!;

    public async ValueTask InitializeAsync()
    {
        // Create a unique temp database for each test
        _databasePath = Path.Combine(Path.GetTempPath(), $"wiretap_test_{Guid.NewGuid()}.db3");
        _database = new WiretapDatabase(_databasePath);
        await _database.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();

        // Clean up test database
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private static HttpRecord CreateTestRecord(
        string method = "GET",
        string url = "https://api.example.com/test",
        int statusCode = 200)
    {
        return new HttpRecord
        {
            Method = method,
            Url = url,
            StatusCode = statusCode,
            IsComplete = true,
            Duration = TimeSpan.FromMilliseconds(150),
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } },
                { "Accept", new[] { "application/json" } }
            },
            RequestBody = "{\"test\": true}",
            RequestSize = 14,
            ResponseHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } }
            },
            ResponseBody = "{\"result\": \"success\"}",
            ResponseSize = 21
        };
    }

    #region Initialization Tests

    [Fact]
    public async Task Initialize_CreatesDatabase()
    {
        // Database should be initialized in InitializeAsync
        Assert.True(File.Exists(_databasePath));
    }

    [Fact]
    public async Task Initialize_IsIdempotent()
    {
        // Calling initialize multiple times should not throw
        await _database.InitializeAsync();
        await _database.InitializeAsync();
        await _database.InitializeAsync();

        Assert.True(File.Exists(_databasePath));
    }

    #endregion

    #region Insert Tests

    [Fact]
    public async Task InsertAsync_AddsRecord()
    {
        var record = CreateTestRecord();

        await _database.InsertAsync(record);

        var count = await _database.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InsertAsync_PreservesAllFields()
    {
        var record = CreateTestRecord();
        record.RequestBody = "Test request body";
        record.ResponseBody = "Test response body";
        record.ErrorMessage = null;
        record.IsComplete = true;

        await _database.InsertAsync(record);

        var retrieved = await _database.GetRecordAsync(record.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(record.Id, retrieved.Id);
        Assert.Equal(record.Method, retrieved.Method);
        Assert.Equal(record.Url, retrieved.Url);
        Assert.Equal(record.StatusCode, retrieved.StatusCode);
        Assert.Equal(record.RequestBody, retrieved.RequestBody);
        Assert.Equal(record.ResponseBody, retrieved.ResponseBody);
        Assert.Equal(record.IsComplete, retrieved.IsComplete);
        Assert.Equal((long)record.Duration.TotalMilliseconds, (long)retrieved.Duration.TotalMilliseconds);
    }

    [Fact]
    public async Task InsertAsync_PreservesHeaders()
    {
        var record = new HttpRecord
        {
            Method = "GET",
            Url = "https://api.example.com/test",
            StatusCode = 200,
            IsComplete = true,
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { "Bearer token" } },
                { "Accept", new[] { "application/json", "text/plain" } }
            }
        };

        await _database.InsertAsync(record);

        var retrieved = await _database.GetRecordAsync(record.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.RequestHeaders.Count);
        Assert.Equal("Bearer token", retrieved.RequestHeaders["Authorization"][0]);
        Assert.Equal(2, retrieved.RequestHeaders["Accept"].Length);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task GetRecordAsync_ReturnsNullForMissingRecord()
    {
        var result = await _database.GetRecordAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRecordsAsync_ReturnsNewestFirst()
    {
        var record1 = CreateTestRecord();
        var record2 = CreateTestRecord();
        var record3 = CreateTestRecord();

        await _database.InsertAsync(record1);
        await Task.Delay(10); // Ensure different timestamps
        await _database.InsertAsync(record2);
        await Task.Delay(10);
        await _database.InsertAsync(record3);

        var records = await _database.GetRecordsAsync();

        Assert.Equal(3, records.Count);
        Assert.True(records[0].Timestamp >= records[1].Timestamp);
        Assert.True(records[1].Timestamp >= records[2].Timestamp);
    }

    [Fact]
    public async Task GetRecordsAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _database.InsertAsync(CreateTestRecord());
        }

        var records = await _database.GetRecordsAsync(limit: 5);

        Assert.Equal(5, records.Count);
    }

    [Fact]
    public async Task GetRecordsAsync_ZeroLimit_ReturnsAll()
    {
        for (int i = 0; i < 5; i++)
        {
            await _database.InsertAsync(CreateTestRecord());
        }

        var records = await _database.GetRecordsAsync(limit: 0);

        Assert.Equal(5, records.Count);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_ModifiesExistingRecord()
    {
        var record = CreateTestRecord();
        await _database.InsertAsync(record);

        record.StatusCode = 500;
        record.ErrorMessage = "Server Error";
        await _database.UpdateAsync(record);

        var retrieved = await _database.GetRecordAsync(record.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(500, retrieved.StatusCode);
        Assert.Equal("Server Error", retrieved.ErrorMessage);
    }

    [Fact]
    public async Task InsertOrReplaceAsync_InsertsNewRecord()
    {
        var record = CreateTestRecord();

        await _database.InsertOrReplaceAsync(record);

        var count = await _database.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InsertOrReplaceAsync_ReplacesExistingRecord()
    {
        var record = CreateTestRecord();
        await _database.InsertAsync(record);

        record.StatusCode = 404;
        await _database.InsertOrReplaceAsync(record);

        var count = await _database.CountAsync();
        Assert.Equal(1, count);

        var retrieved = await _database.GetRecordAsync(record.Id);
        Assert.Equal(404, retrieved!.StatusCode);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_RemovesRecord()
    {
        var record = CreateTestRecord();
        await _database.InsertAsync(record);

        var deleted = await _database.DeleteAsync(record.Id);

        Assert.Equal(1, deleted);
        Assert.Equal(0, await _database.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsZeroForMissingRecord()
    {
        var deleted = await _database.DeleteAsync(Guid.NewGuid());

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesAllRecords()
    {
        for (int i = 0; i < 5; i++)
        {
            await _database.InsertAsync(CreateTestRecord());
        }

        var deleted = await _database.DeleteAllAsync();

        Assert.Equal(5, deleted);
        Assert.Equal(0, await _database.CountAsync());
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldRecords()
    {
        // Insert records with different timestamps
        for (int i = 0; i < 5; i++)
        {
            await _database.InsertAsync(CreateTestRecord());
            await Task.Delay(10);
        }

        var cutoff = DateTime.UtcNow.AddMilliseconds(-25);
        await _database.DeleteOlderThanAsync(cutoff);

        var remaining = await _database.CountAsync();
        Assert.True(remaining > 0 && remaining < 5);
    }

    [Fact]
    public async Task TrimToCountAsync_KeepsNewestRecords()
    {
        for (int i = 0; i < 10; i++)
        {
            await _database.InsertAsync(CreateTestRecord());
            await Task.Delay(5);
        }

        await _database.TrimToCountAsync(5);

        var count = await _database.CountAsync();
        Assert.True(count <= 5);
    }

    #endregion

    #region Search and Filter Tests

    [Fact]
    public async Task SearchByUrlAsync_FindsMatchingRecords()
    {
        await _database.InsertAsync(CreateTestRecord(url: "https://api.example.com/users"));
        await _database.InsertAsync(CreateTestRecord(url: "https://api.example.com/products"));
        await _database.InsertAsync(CreateTestRecord(url: "https://other.com/data"));

        var results = await _database.SearchByUrlAsync("example.com");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchByUrlAsync_IsCaseInsensitive()
    {
        await _database.InsertAsync(CreateTestRecord(url: "https://API.EXAMPLE.COM/users"));

        var results = await _database.SearchByUrlAsync("example.com");

        Assert.Single(results);
    }

    [Fact]
    public async Task GetByMethodAsync_FiltersCorrectly()
    {
        await _database.InsertAsync(CreateTestRecord(method: "GET"));
        await _database.InsertAsync(CreateTestRecord(method: "GET"));
        await _database.InsertAsync(CreateTestRecord(method: "POST"));
        await _database.InsertAsync(CreateTestRecord(method: "PUT"));

        var getRecords = await _database.GetByMethodAsync("GET");
        var postRecords = await _database.GetByMethodAsync("POST");

        Assert.Equal(2, getRecords.Count);
        Assert.Single(postRecords);
    }

    [Fact]
    public async Task GetByStatusRangeAsync_FiltersCorrectly()
    {
        await _database.InsertAsync(CreateTestRecord(statusCode: 200));
        await _database.InsertAsync(CreateTestRecord(statusCode: 201));
        await _database.InsertAsync(CreateTestRecord(statusCode: 400));
        await _database.InsertAsync(CreateTestRecord(statusCode: 500));

        var successRecords = await _database.GetByStatusRangeAsync(200, 300);
        var errorRecords = await _database.GetByStatusRangeAsync(400, 600);

        Assert.Equal(2, successRecords.Count);
        Assert.Equal(2, errorRecords.Count);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentInserts_NoDataCorruption()
    {
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => _database.InsertAsync(CreateTestRecord()));

        await Task.WhenAll(tasks);

        var count = await _database.CountAsync();
        Assert.Equal(50, count);
    }

    [Fact]
    public async Task ConcurrentReadsAndWrites_NoExceptions()
    {
        // Pre-populate
        for (int i = 0; i < 10; i++)
        {
            await _database.InsertAsync(CreateTestRecord());
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var exceptions = new List<Exception>();

        var writeTasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await _database.InsertAsync(CreateTestRecord());
                    await Task.Delay(10, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        var readTasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await _database.GetRecordsAsync(10);
                    await Task.Delay(10, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { exceptions.Add(ex); }
        });

        await Task.WhenAll(writeTasks.Concat(readTasks));

        Assert.Empty(exceptions);
    }

    #endregion
}

public class HttpRecordEntityTests
{
    [Fact]
    public void FromRecord_CreatesEntity()
    {
        var record = new HttpRecord
        {
            Method = "POST",
            Url = "https://api.example.com/data",
            StatusCode = 201,
            Duration = TimeSpan.FromMilliseconds(123),
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Content-Type", new[] { "application/json" } }
            },
            RequestBody = "{\"name\": \"test\"}",
            IsComplete = true
        };

        var entity = HttpRecordEntity.FromRecord(record);

        Assert.Equal(record.Id.ToString(), entity.Id);
        Assert.Equal(record.Method, entity.Method);
        Assert.Equal(record.Url, entity.Url);
        Assert.Equal(record.StatusCode, entity.StatusCode);
        Assert.Equal(123, entity.DurationMs);
        Assert.Contains("Content-Type", entity.RequestHeadersJson);
    }

    [Fact]
    public void ToRecord_RestoresRecord()
    {
        var originalRecord = new HttpRecord
        {
            Method = "DELETE",
            Url = "https://api.example.com/items/42",
            StatusCode = 204,
            Duration = TimeSpan.FromMilliseconds(50),
            RequestHeaders = new Dictionary<string, string[]>
            {
                { "Authorization", new[] { "Bearer token123" } }
            },
            ResponseHeaders = new Dictionary<string, string[]>(),
            IsComplete = true
        };

        var entity = HttpRecordEntity.FromRecord(originalRecord);
        var restoredRecord = entity.ToRecord();

        Assert.Equal(originalRecord.Id, restoredRecord.Id);
        Assert.Equal(originalRecord.Method, restoredRecord.Method);
        Assert.Equal(originalRecord.Url, restoredRecord.Url);
        Assert.Equal(originalRecord.StatusCode, restoredRecord.StatusCode);
        Assert.Equal((long)originalRecord.Duration.TotalMilliseconds, (long)restoredRecord.Duration.TotalMilliseconds);
        Assert.Equal("Bearer token123", restoredRecord.RequestHeaders["Authorization"][0]);
    }

    [Fact]
    public void ToRecord_HandlesNullHeaders()
    {
        var entity = new HttpRecordEntity
        {
            Id = Guid.NewGuid().ToString(),
            Method = "GET",
            Url = "https://example.com",
            RequestHeadersJson = null,
            ResponseHeadersJson = null
        };

        var record = entity.ToRecord();

        Assert.NotNull(record.RequestHeaders);
        Assert.NotNull(record.ResponseHeaders);
        Assert.Empty(record.RequestHeaders);
        Assert.Empty(record.ResponseHeaders);
    }

    [Fact]
    public void ToRecord_HandlesInvalidJsonHeaders()
    {
        var entity = new HttpRecordEntity
        {
            Id = Guid.NewGuid().ToString(),
            Method = "GET",
            Url = "https://example.com",
            RequestHeadersJson = "invalid json {{{",
            ResponseHeadersJson = "also invalid"
        };

        var record = entity.ToRecord();

        Assert.NotNull(record.RequestHeaders);
        Assert.Empty(record.RequestHeaders);
    }
}
