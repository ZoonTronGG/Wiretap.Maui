using System.Threading.Channels;
using Wiretap.Maui.Database;

namespace Wiretap.Maui.Core;

/// <summary>
/// Hybrid store combining in-memory cache with SQLite persistence.
/// Provides fast UI access via memory cache while persisting all records to SQLite.
/// </summary>
public class HybridWiretapStore : IWiretapStore, IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly List<HttpRecord> _memoryCache = new();
    private readonly int _memoryCacheSize;
    private readonly WiretapDatabase _database;
    private readonly Channel<HttpRecord> _writeQueue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writeWorker;
    private readonly Task _cleanupWorker;
    private readonly int _retentionDays;
    private readonly int _maxPersistedRequests;
    private bool _initialized;

    /// <inheritdoc />
    public event Action<HttpRecord>? OnRecordAdded;

    /// <inheritdoc />
    public event Action? OnRecordsCleared;

    /// <summary>
    /// Creates a new HybridWiretapStore with the specified options.
    /// </summary>
    /// <param name="options">Wiretap configuration options.</param>
    public HybridWiretapStore(WiretapOptions options)
    {
        _memoryCacheSize = options.MemoryCacheSize;
        _retentionDays = options.RetentionDays;
        _maxPersistedRequests = options.MaxPersistedRequests;

        var dbPath = options.DatabasePath ?? WiretapDatabase.GetDefaultDatabasePath();
        _database = new WiretapDatabase(dbPath);

        // Unbounded channel for background writes
        _writeQueue = Channel.CreateUnbounded<HttpRecord>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Start background workers
        _writeWorker = Task.Run(ProcessWriteQueueAsync);
        _cleanupWorker = Task.Run(RunCleanupWorkerAsync);
    }

    /// <summary>
    /// Creates a new HybridWiretapStore with a custom database instance (for testing).
    /// </summary>
    internal HybridWiretapStore(WiretapOptions options, WiretapDatabase database)
    {
        _memoryCacheSize = options.MemoryCacheSize;
        _retentionDays = options.RetentionDays;
        _maxPersistedRequests = options.MaxPersistedRequests;
        _database = database;

        _writeQueue = Channel.CreateUnbounded<HttpRecord>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _writeWorker = Task.Run(ProcessWriteQueueAsync);
        _cleanupWorker = Task.Run(RunCleanupWorkerAsync);
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _memoryCache.Count;
            }
        }
    }

    /// <summary>
    /// Initializes the store by loading recent records from the database into memory.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _database.InitializeAsync();

        // Load recent records into memory cache
        var recentRecords = await _database.GetRecordsAsync(_memoryCacheSize);

        lock (_lock)
        {
            _memoryCache.Clear();
            _memoryCache.AddRange(recentRecords);
            _initialized = true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HttpRecord> GetRecords()
    {
        lock (_lock)
        {
            // Return from memory cache (already ordered newest first from DB)
            return _memoryCache.OrderByDescending(r => r.Timestamp).ToList();
        }
    }

    /// <inheritdoc />
    public HttpRecord? GetRecord(Guid id)
    {
        lock (_lock)
        {
            // Check memory cache first
            var cached = _memoryCache.FirstOrDefault(r => r.Id == id);
            if (cached != null)
                return cached;
        }

        // Fall back to database (blocking for interface compatibility)
        return _database.GetRecordAsync(id).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets a record by ID asynchronously, checking memory first then database.
    /// </summary>
    public async Task<HttpRecord?> GetRecordAsync(Guid id)
    {
        lock (_lock)
        {
            var cached = _memoryCache.FirstOrDefault(r => r.Id == id);
            if (cached != null)
                return cached;
        }

        return await _database.GetRecordAsync(id);
    }

    /// <inheritdoc />
    public void Add(HttpRecord record)
    {
        lock (_lock)
        {
            // Ring buffer: remove oldest if at capacity
            while (_memoryCache.Count >= _memoryCacheSize)
            {
                // Remove oldest (first in the list after sorting by timestamp ascending)
                var oldest = _memoryCache.OrderBy(r => r.Timestamp).First();
                _memoryCache.Remove(oldest);
            }

            _memoryCache.Add(record);
        }

        // Queue for background write to database
        _writeQueue.Writer.TryWrite(record);

        // Raise event outside lock
        OnRecordAdded?.Invoke(record);
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _memoryCache.Clear();
        }

        // Clear database in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _database.DeleteAllAsync();
            }
            catch
            {
                // Log error in production, ignore for now
            }
        });

        OnRecordsCleared?.Invoke();
    }

    /// <summary>
    /// Clears all records asynchronously.
    /// </summary>
    public async Task ClearAsync()
    {
        lock (_lock)
        {
            _memoryCache.Clear();
        }

        await _database.DeleteAllAsync();

        OnRecordsCleared?.Invoke();
    }

    /// <summary>
    /// Searches records by URL. Queries SQLite for comprehensive results.
    /// </summary>
    public async Task<List<HttpRecord>> SearchByUrlAsync(string searchText, int limit = 100)
    {
        await EnsureInitializedAsync();
        return await _database.SearchByUrlAsync(searchText, limit);
    }

    /// <summary>
    /// Gets records filtered by HTTP method. Queries SQLite.
    /// </summary>
    public async Task<List<HttpRecord>> GetByMethodAsync(string method, int limit = 100)
    {
        await EnsureInitializedAsync();
        return await _database.GetByMethodAsync(method, limit);
    }

    /// <summary>
    /// Gets records filtered by status code range. Queries SQLite.
    /// </summary>
    public async Task<List<HttpRecord>> GetByStatusRangeAsync(int minStatus, int maxStatus, int limit = 100)
    {
        await EnsureInitializedAsync();
        return await _database.GetByStatusRangeAsync(minStatus, maxStatus, limit);
    }

    /// <summary>
    /// Gets total count of records in the database.
    /// </summary>
    public async Task<int> GetTotalCountAsync()
    {
        await EnsureInitializedAsync();
        return await _database.CountAsync();
    }

    /// <summary>
    /// Gets records from database with pagination.
    /// </summary>
    public async Task<List<HttpRecord>> GetRecordsAsync(int limit = 0)
    {
        await EnsureInitializedAsync();
        return await _database.GetRecordsAsync(limit);
    }

    private async Task ProcessWriteQueueAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            var writeCount = 0;
            await foreach (var record in _writeQueue.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    await _database.InsertOrReplaceAsync(record);
                    writeCount++;

                    // Periodically enforce max persisted records limit (every 100 writes)
                    if (writeCount % 100 == 0)
                    {
                        await TrimToMaxPersistedAsync();
                    }
                }
                catch (Exception)
                {
                    // Log error in production, continue processing queue
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    private async Task TrimToMaxPersistedAsync()
    {
        try
        {
            var count = await _database.CountAsync();
            if (count > _maxPersistedRequests)
            {
                await _database.TrimToCountAsync(_maxPersistedRequests);
            }
        }
        catch (Exception)
        {
            // Log error in production
        }
    }

    private async Task RunCleanupWorkerAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            // Run cleanup on startup
            await CleanupOldRecordsAsync();

            // Then periodically (every hour)
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                await CleanupOldRecordsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    private async Task CleanupOldRecordsAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
            await _database.DeleteOlderThanAsync(cutoffDate);
        }
        catch (Exception)
        {
            // Log error in production
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
            await InitializeAsync();
    }

    /// <summary>
    /// Waits for all pending writes to complete.
    /// Useful for testing and graceful shutdown.
    /// </summary>
    public async Task FlushAsync()
    {
        _writeQueue.Writer.Complete();
        await _writeWorker;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        try
        {
            _writeQueue.Writer.Complete();
            await Task.WhenAny(_writeWorker, Task.Delay(TimeSpan.FromSeconds(5)));
            await Task.WhenAny(_cleanupWorker, Task.Delay(TimeSpan.FromSeconds(1)));
        }
        catch
        {
            // Ignore errors during shutdown
        }

        await _database.DisposeAsync();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
