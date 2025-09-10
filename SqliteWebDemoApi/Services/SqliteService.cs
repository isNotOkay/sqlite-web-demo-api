using SqliteWebDemoApi.Constants;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApi.Services;

public sealed class SqliteService(ISqliteRepository repo) : ISqliteService
{
    public Task<(IReadOnlyList<SqliteRelationInfo> Items, long Total)> ListTablesAsync(CancellationToken ct) =>
        ListRelationsAsync(SqliteQueries.ListTables, ct);

    public Task<(IReadOnlyList<SqliteRelationInfo> Items, long Total)> ListViewsAsync(CancellationToken ct) =>
        ListRelationsAsync(SqliteQueries.ListViews, ct);

    public async Task<PageResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, string? sortBy, string? sortDir, CancellationToken ct)
    {
        SqliteIdentifierUtil.EnsureValid(tableId, nameof(tableId));
        if (!await repo.ObjectExistsAsync("table", tableId, ct))
            throw new KeyNotFoundException($"Table \"{tableId}\" not found.");

        var quoted = SqliteIdentifierUtil.Quote(tableId);
        var totalRows = await repo.CountRowsAsync(quoted, ct);

        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            PaginatorUtil.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildPage(normalizedPage, normalizedPageSize, 0, totalPages, []);

        // Sorting
        var (orderByColumn, orderByDesc, addRowIdTiebreaker) =
            await BuildSortAsync(quoted, isView: false, sortBy, sortDir, tableId, ct);

        var rows = await repo.GetPageAsync(
            quoted, orderByColumn, orderByDesc, addRowIdTiebreaker, normalizedPageSize, offset, ct);

        return BuildPage(normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    public async Task<PageResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, string? sortBy, string? sortDir, CancellationToken ct)
    {
        SqliteIdentifierUtil.EnsureValid(viewId, nameof(viewId));
        if (!await repo.ObjectExistsAsync("view", viewId, ct))
            throw new KeyNotFoundException($"View \"{viewId}\" not found.");

        var quoted = SqliteIdentifierUtil.Quote(viewId);
        var totalRows = await repo.CountRowsAsync(quoted, ct);

        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            PaginatorUtil.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildPage(normalizedPage, normalizedPageSize, 0, totalPages, []);

        var (orderByColumn, orderByDesc, addRowIdTiebreaker) =
            await BuildSortAsync(quoted, isView: true, sortBy, sortDir, viewId, ct);

        var rows = await repo.GetPageAsync(
            quoted, orderByColumn, orderByDesc, addRowIdTiebreaker, normalizedPageSize, offset, ct);

        return BuildPage(normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }
    
    private async Task<(string? OrderByColumn, bool OrderByDesc, bool AddRowIdTiebreaker)> BuildSortAsync(
        string quotedName, bool isView, string? sortBy, string? sortDir, string rawName, CancellationToken ct)
    {
        // Direction
        var dir = (sortDir ?? "asc");
        var desc = dir.Equals("desc", StringComparison.OrdinalIgnoreCase);
        if (!dir.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !dir.Equals("desc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("sortDir must be 'asc' or 'desc'.");

        // If client did not request a column, fall back to rowid for tables (if available), none for views.
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            var hasRowId = !isView && !await repo.IsWithoutRowIdAsync(rawName, ct);
            return (null, desc, addRowIdTiebreaker: hasRowId);
        }

        // Validate column exists
        var columns = await repo.GetColumnNamesAsync(quotedName, ct);
        var matched = columns.FirstOrDefault(c => c.Equals(sortBy, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
            throw new ArgumentException($"Column '{sortBy}' does not exist on \"{rawName}\".");

        // Quote the matched column (preserve exact case as returned from schema)
        var quotedColumn = SqliteIdentifierUtil.Quote(matched);

        // Add a deterministic tiebreaker if table has rowid
        var addRowId = !isView && !await repo.IsWithoutRowIdAsync(rawName, ct);

        return (quotedColumn, desc, addRowId);
    }

    private async Task<(IReadOnlyList<SqliteRelationInfo> Items, long Total)> ListRelationsAsync(string listSql, CancellationToken ct)
    {
        var items = await repo.ListRelationsAsync(listSql, ct);
        return (items, (long)items.Count);
    }

    private static PageResult<Dictionary<string, object?>> BuildPage(
        int page,
        int pageSize,
        long total,
        int totalPages,
        IReadOnlyList<Dictionary<string, object?>> items) =>
        new()
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            Items = items
        };
}
