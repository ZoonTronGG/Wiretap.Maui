using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wiretap.Maui.Core;
using Wiretap.Maui.Services;

namespace Wiretap.Maui.UI;

/// <summary>
/// ViewModel for the WiretapPage, managing the list of captured HTTP records.
/// </summary>
public partial class WiretapViewModel : ObservableObject
{
    private readonly IWiretapStore _store;
    private readonly IDispatcher _dispatcher;
    private readonly SearchService _searchService = new();
    private readonly RecordFilter _filter = new();

    private HttpRecord? _selectedRecord;
    private CancellationTokenSource? _searchDebounceTokenSource;

    /// <summary>
    /// Whether the records list is empty.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(ShowRecordsList))]
    private bool _isEmpty = true;

    /// <summary>
    /// Search text for filtering records.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Method filter chips for GET, POST, PUT, DELETE, PATCH.
    /// </summary>
    public List<FilterChip> MethodChips { get; } = new()
    {
        new FilterChip { Text = "GET", Value = "GET", SelectedColor = Color.FromArgb("#2196F3") },
        new FilterChip { Text = "POST", Value = "POST", SelectedColor = Color.FromArgb("#4CAF50") },
        new FilterChip { Text = "PUT", Value = "PUT", SelectedColor = Color.FromArgb("#FF9800") },
        new FilterChip { Text = "DELETE", Value = "DELETE", SelectedColor = Color.FromArgb("#F44336") },
        new FilterChip { Text = "PATCH", Value = "PATCH", SelectedColor = Color.FromArgb("#9C27B0") }
    };

    /// <summary>
    /// Status filter chips for 2xx, 4xx, 5xx, and failed requests.
    /// </summary>
    public List<FilterChip> StatusChips { get; } = new()
    {
        new FilterChip { Text = "2xx ✓", Value = "2", SelectedColor = Color.FromArgb("#4CAF50") },
        new FilterChip { Text = "4xx ⚠", Value = "4", SelectedColor = Color.FromArgb("#FF9800") },
        new FilterChip { Text = "5xx ✕", Value = "5", SelectedColor = Color.FromArgb("#F44336") },
        new FilterChip { Text = "Failed", Value = "0", SelectedColor = Color.FromArgb("#9E9E9E") }
    };

    /// <summary>
    /// Debounce delay for search input in milliseconds.
    /// </summary>
    private const int SearchDebounceDelayMs = 300;

    /// <summary>
    /// All HTTP records from the store (unfiltered).
    /// </summary>
    private readonly List<HttpRecord> _allRecords = new();

    /// <summary>
    /// Observable collection of HTTP records for binding (filtered).
    /// </summary>
    public ObservableCollection<HttpRecord> Records { get; } = new();

    /// <summary>
    /// Total number of unfiltered records.
    /// </summary>
    public int TotalCount => _allRecords.Count;

    /// <summary>
    /// Number of records matching the current filter.
    /// </summary>
    public int FilteredCount => Records.Count;

    /// <summary>
    /// Whether a filter is currently active.
    /// </summary>
    public bool IsFilterActive => !_filter.IsEmpty;

    /// <summary>
    /// Whether there are no results due to filtering (has records but none match filter).
    /// </summary>
    public bool HasNoSearchResults => !IsEmpty && IsFilterActive && FilteredCount == 0;

    /// <summary>
    /// Whether to show the records list.
    /// </summary>
    public bool ShowRecordsList => !IsEmpty && FilteredCount > 0;

    /// <summary>
    /// Display text for the record count (e.g., "5 of 10 requests" or "10 requests").
    /// </summary>
    public string CountDisplay => IsFilterActive
        ? $"{FilteredCount} of {TotalCount} requests"
        : $"{TotalCount} requests";

    /// <summary>
    /// Currently selected record (for navigation to detail).
    /// </summary>
    public HttpRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
            {
                if (value != null)
                {
                    // Navigate to detail page
                    NavigateToDetail(value);
                    // Reset selection after navigation
                    _selectedRecord = null;
                    OnPropertyChanged(nameof(SelectedRecord));
                }
            }
        }
    }


    /// <summary>
    /// Command to clear all records.
    /// </summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>
    /// Command to refresh the records list.
    /// </summary>
    public IRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Command to clear the search text.
    /// </summary>
    public IRelayCommand ClearSearchCommand { get; }

    /// <summary>
    /// Command to toggle a method filter chip.
    /// </summary>
    public IRelayCommand<FilterChip> ToggleMethodCommand { get; }

    /// <summary>
    /// Command to toggle a status filter chip.
    /// </summary>
    public IRelayCommand<FilterChip> ToggleStatusCommand { get; }

    public WiretapViewModel(IWiretapStore store, IDispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;

        ClearCommand = new RelayCommand(OnClear);
        RefreshCommand = new RelayCommand(OnRefresh);
        ClearSearchCommand = new RelayCommand(OnClearSearch);
        ToggleMethodCommand = new RelayCommand<FilterChip>(OnToggleMethod);
        ToggleStatusCommand = new RelayCommand<FilterChip>(OnToggleStatus);
        foreach (var chip in MethodChips)
        {
            chip.ToggleCommand = ToggleMethodCommand;
        }
        foreach (var chip in StatusChips)
        {
            chip.ToggleCommand = ToggleStatusCommand;
        }

        // Subscribe to store events
        _store.OnRecordAdded += OnRecordAdded;
        _store.OnRecordsCleared += OnRecordsCleared;

        // Load initial records
        LoadRecords();
    }

    private void LoadRecords()
    {
        _allRecords.Clear();
        _allRecords.AddRange(_store.GetRecords());
        ApplyFilter();
    }

    private void OnRecordAdded(HttpRecord record)
    {
        // Ensure we're on the main thread
        _dispatcher.Dispatch(() =>
        {
            // Insert at beginning of all records (newest first)
            _allRecords.Insert(0, record);

            // If record matches filter, add to visible collection
            if (_searchService.Matches(record, _filter))
            {
                Records.Insert(0, record);
            }

            UpdateCounts();
        });
    }

    private void OnRecordsCleared()
    {
        _dispatcher.Dispatch(() =>
        {
            _allRecords.Clear();
            Records.Clear();
            UpdateCounts();
        });
    }

    private void OnClear()
    {
        _store.Clear();
    }

    private void OnRefresh()
    {
        LoadRecords();
    }

    private void OnClearSearch()
    {
        SearchText = string.Empty;
        // Also clear method filters
        foreach (var chip in MethodChips)
        {
            chip.IsSelected = false;
        }
        _filter.Methods.Clear();
        // Also clear status filters
        foreach (var chip in StatusChips)
        {
            chip.IsSelected = false;
        }
        _filter.StatusGroups.Clear();
        ApplyFilter();
    }

    private void OnToggleMethod(FilterChip? chip)
    {
        if (chip == null)
            return;

        chip.IsSelected = !chip.IsSelected;

        if (chip.IsSelected)
        {
            _filter.AddMethod(chip.Value);
        }
        else
        {
            _filter.RemoveMethod(chip.Value);
        }

        ApplyFilter();
    }

    private void OnToggleStatus(FilterChip? chip)
    {
        if (chip == null)
            return;

        chip.IsSelected = !chip.IsSelected;

        if (int.TryParse(chip.Value, out var statusGroup))
        {
            if (chip.IsSelected)
            {
                _filter.AddStatusGroup(statusGroup);
            }
            else
            {
                _filter.RemoveStatusGroup(statusGroup);
            }
        }

        ApplyFilter();
    }

    /// <summary>
    /// Called when SearchText changes to trigger debounced filtering.
    /// </summary>
    partial void OnSearchTextChanged(string value) => DebouncedSearch();

    private void DebouncedSearch()
    {
        // Cancel any pending search
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();
        var token = _searchDebounceTokenSource.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SearchDebounceDelayMs, token);

                if (!token.IsCancellationRequested)
                {
                    _dispatcher.Dispatch(ApplyFilter);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation
            }
        }, token);
    }

    /// <summary>
    /// Applies the current filter to the records collection.
    /// </summary>
    internal void ApplyFilter()
    {
        // Update filter with current search text
        _filter.SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

        // Filter records
        var filtered = _searchService.Filter(_allRecords, _filter);

        // Update observable collection
        Records.Clear();
        foreach (var record in filtered)
        {
            Records.Add(record);
        }

        UpdateCounts();
    }

    private void UpdateCounts()
    {
        IsEmpty = _allRecords.Count == 0;
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(ShowRecordsList));
        OnPropertyChanged(nameof(CountDisplay));
    }

    private async void NavigateToDetail(HttpRecord record)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(WiretapDetailPage), new Dictionary<string, object>
            {
                { "RecordId", record.Id }
            });
        }
        catch
        {
            // Navigation might fail if Shell is not configured
            // In that case, try a simple push navigation using the active window
            try
            {
                var detailPage = new WiretapDetailPage(_store);
                detailPage.LoadRecord(record.Id);

                var navigation = GetCurrentNavigation();
                if (navigation != null)
                {
                    await navigation.PushAsync(detailPage);
                }
            }
            catch
            {
                // Ignore navigation errors
            }
        }
    }

    private static INavigation? GetCurrentNavigation()
    {
        // Try to get navigation from the first window's page
        var window = Application.Current?.Windows.FirstOrDefault();
        return window?.Page?.Navigation;
    }

    /// <summary>
    /// Clean up event subscriptions.
    /// </summary>
    public void Dispose()
    {
        _store.OnRecordAdded -= OnRecordAdded;
        _store.OnRecordsCleared -= OnRecordsCleared;
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource?.Dispose();
    }
}
