using Wiretap.Maui.Core;

namespace Wiretap.Maui.Services;

/// <summary>
/// Service for filtering and searching HTTP records.
/// </summary>
public class SearchService
{
    /// <summary>
    /// Filters a collection of HTTP records based on the specified criteria.
    /// </summary>
    /// <param name="records">The records to filter.</param>
    /// <param name="filter">The filter criteria.</param>
    /// <returns>Filtered records matching all criteria.</returns>
    public IEnumerable<HttpRecord> Filter(IEnumerable<HttpRecord> records, RecordFilter filter)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.IsEmpty)
            return records;

        return records.Where(record => Matches(record, filter));
    }

    /// <summary>
    /// Checks if a single record matches the filter criteria.
    /// </summary>
    /// <param name="record">The record to check.</param>
    /// <param name="filter">The filter criteria.</param>
    /// <returns>True if the record matches all filter criteria.</returns>
    public bool Matches(HttpRecord record, RecordFilter filter)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(filter);

        // All criteria must match (AND logic)
        return MatchesSearchText(record, filter.SearchText) &&
               MatchesMethods(record, filter.Methods) &&
               MatchesStatusGroups(record, filter.StatusGroups);
    }

    /// <summary>
    /// Checks if a record matches the search text.
    /// Searches URL, request body, and response body (case-insensitive).
    /// </summary>
    private static bool MatchesSearchText(HttpRecord record, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        var search = searchText.Trim();

        // Search in URL
        if (ContainsIgnoreCase(record.Url, search))
            return true;

        // Search in request body
        if (ContainsIgnoreCase(record.RequestBody, search))
            return true;

        // Search in response body
        if (ContainsIgnoreCase(record.ResponseBody, search))
            return true;

        // Search in request headers (header names and values)
        foreach (var (headerName, headerValues) in record.RequestHeaders)
        {
            if (ContainsIgnoreCase(headerName, search))
                return true;

            foreach (var value in headerValues)
            {
                if (ContainsIgnoreCase(value, search))
                    return true;
            }
        }

        // Search in response headers
        foreach (var (headerName, headerValues) in record.ResponseHeaders)
        {
            if (ContainsIgnoreCase(headerName, search))
                return true;

            foreach (var value in headerValues)
            {
                if (ContainsIgnoreCase(value, search))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a record's HTTP method matches the filter.
    /// </summary>
    private static bool MatchesMethods(HttpRecord record, HashSet<string> methods)
    {
        // Empty set means no filter - include all methods
        if (methods.Count == 0)
            return true;

        return methods.Contains(record.Method);
    }

    /// <summary>
    /// Checks if a record's status code group matches the filter.
    /// </summary>
    private static bool MatchesStatusGroups(HttpRecord record, HashSet<int> statusGroups)
    {
        // Empty set means no filter - include all status groups
        if (statusGroups.Count == 0)
            return true;

        // Handle failed/incomplete requests (status group 0)
        if (!record.IsComplete)
            return statusGroups.Contains(0);

        // Get the status group (first digit of status code)
        var statusGroup = record.StatusCode / 100;
        return statusGroups.Contains(statusGroup);
    }

    /// <summary>
    /// Case-insensitive contains check.
    /// </summary>
    private static bool ContainsIgnoreCase(string? text, string search)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
