namespace Grsms.Application.Common;

/// <summary>
/// One page of a long list (e.g. the audit trail), plus the numbers a pager needs.
/// </summary>
/// <param name="Items">The rows on this page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
/// <param name="TotalCount">Rows across all pages.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>How many pages there are in total (at least 1, so "page 1 of 1" shows for an empty list).</summary>
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
