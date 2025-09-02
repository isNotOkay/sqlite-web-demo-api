namespace SqliteWebDemoApi.Utilities;

public static class PaginatorUtil
{
    private const int MaxPageSize = 1000;

    /// <summary>
    /// Normalizes paging inputs and returns the effective Page, PageSize, TotalPages, and Offset.
    /// - Page is clamped to [1..TotalPages]
    /// - PageSize is clamped to [1..MaxPageSize]
    /// - TotalPages is at least 1 (even when totalRows == 0) to simplify callers
    /// </summary>
    public static (int Page, int PageSize, int TotalPages, int Offset) Paginate(
        int requestedPage, int requestedPageSize, long totalRows)
    {
        var pageSize = Math.Clamp(requestedPageSize, 1, MaxPageSize);
        var totalPages = (int)Math.Max(1, Math.Ceiling(totalRows / (double)pageSize));
        var page = Math.Clamp(requestedPage, 1, totalPages);
        var offset = (page - 1) * pageSize;
        return (page, pageSize, totalPages, offset);
    }
}