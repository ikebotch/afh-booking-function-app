namespace AFH.Acs.Recorder.Models.V1;

/// <summary>
/// Standard paginated response container used across Leads, Advisers, Meetings, etc.
/// </summary>
public class PagedResult<T>
{
    /// <summary>
    /// The current page of results.
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Page number (1-indexed).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Page size used for this query.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Computed total number of pages.
    /// </summary>
    public int TotalPages =>
        PageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}