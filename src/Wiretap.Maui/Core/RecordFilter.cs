namespace Wiretap.Maui.Core;

/// <summary>
/// Represents filter criteria for searching HTTP records.
/// </summary>
public class RecordFilter
{
    /// <summary>
    /// Text to search for in URL, request body, and response body.
    /// Search is case-insensitive.
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// HTTP methods to include (e.g., GET, POST, PUT, DELETE).
    /// Empty set means all methods are included.
    /// </summary>
    public HashSet<string> Methods { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Status code groups to include (e.g., 2, 4, 5 for 2xx, 4xx, 5xx).
    /// Empty set means all status groups are included.
    /// Use 0 to include failed/incomplete requests.
    /// </summary>
    public HashSet<int> StatusGroups { get; set; } = new();

    /// <summary>
    /// Creates an empty filter that matches all records.
    /// </summary>
    public static RecordFilter Empty => new();

    /// <summary>
    /// Returns true if no filter criteria are set.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(SearchText) &&
        Methods.Count == 0 &&
        StatusGroups.Count == 0;

    /// <summary>
    /// Creates a filter with the specified search text.
    /// </summary>
    public static RecordFilter WithSearchText(string searchText) =>
        new() { SearchText = searchText };

    /// <summary>
    /// Creates a filter for specific HTTP methods.
    /// </summary>
    public static RecordFilter WithMethods(params string[] methods) =>
        new() { Methods = new HashSet<string>(methods, StringComparer.OrdinalIgnoreCase) };

    /// <summary>
    /// Creates a filter for specific status code groups.
    /// </summary>
    /// <param name="groups">Status groups (e.g., 2 for 2xx, 4 for 4xx, 5 for 5xx, 0 for failed)</param>
    public static RecordFilter WithStatusGroups(params int[] groups) =>
        new() { StatusGroups = new HashSet<int>(groups) };

    /// <summary>
    /// Adds a method to the filter.
    /// </summary>
    public RecordFilter AddMethod(string method)
    {
        Methods.Add(method);
        return this;
    }

    /// <summary>
    /// Adds a status group to the filter.
    /// </summary>
    /// <param name="group">Status group (2 for 2xx, 4 for 4xx, 5 for 5xx, 0 for failed)</param>
    public RecordFilter AddStatusGroup(int group)
    {
        StatusGroups.Add(group);
        return this;
    }

    /// <summary>
    /// Removes a method from the filter.
    /// </summary>
    public RecordFilter RemoveMethod(string method)
    {
        Methods.Remove(method);
        return this;
    }

    /// <summary>
    /// Removes a status group from the filter.
    /// </summary>
    public RecordFilter RemoveStatusGroup(int group)
    {
        StatusGroups.Remove(group);
        return this;
    }

    /// <summary>
    /// Clears all filter criteria.
    /// </summary>
    public RecordFilter Clear()
    {
        SearchText = null;
        Methods.Clear();
        StatusGroups.Clear();
        return this;
    }
}
