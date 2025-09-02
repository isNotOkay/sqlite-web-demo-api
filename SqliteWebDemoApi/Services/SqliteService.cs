using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApi.Services;

public sealed class SqliteService(ISqliteRepository repo) : ISqliteService
{
    public Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListTablesAsync(CancellationToken ct) =>
        ListRelationsAsync(SqliteQueries.ListTables, ct);

    public Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListViewsAsync(CancellationToken ct) =>
        ListRelationsAsync(SqliteQueries.ListViews, ct);

    public async Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, CancellationToken ct)
    {
        SqliteIdentifiers.EnsureValid(tableId, nameof(tableId));
        if (!await repo.ObjectExistsAsync("table", tableId, ct))
            throw new KeyNotFoundException($"Table \"{tableId}\" not found.");

        var quoted = SqliteIdentifiers.Quote(tableId);
        var totalRows = await repo.CountRowsAsync(quoted, ct);

        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            Paginator.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildPage("table", tableId, normalizedPage, normalizedPageSize, 0, totalPages, []);

        var orderByRowId = !await repo.IsWithoutRowIdAsync(tableId, ct);
        var rows = await repo.GetPageAsync(quoted, orderByRowId, normalizedPageSize, offset, ct);

        return BuildPage("table", tableId, normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    public async Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, CancellationToken ct)
    {
        SqliteIdentifiers.EnsureValid(viewId, nameof(viewId));
        if (!await repo.ObjectExistsAsync("view", viewId, ct))
            throw new KeyNotFoundException($"View \"{viewId}\" not found.");

        var quoted = SqliteIdentifiers.Quote(viewId);
        var totalRows = await repo.CountRowsAsync(quoted, ct);

        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            Paginator.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildPage("view", viewId, normalizedPage, normalizedPageSize, 0, totalPages, []);

        // Views never use rowid ordering
        var rows = await repo.GetPageAsync(quoted, orderByRowId: false, normalizedPageSize, offset, ct);

        return BuildPage("view", viewId, normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    private async Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListRelationsAsync(string listSql, CancellationToken ct)
    {
        var items = await repo.ListRelationsAsync(listSql, ct);
        return (items, items.Count);
    }

    private static PagedResult<Dictionary<string, object?>> BuildPage(
        string type,
        string name,
        int page,
        int pageSize,
        long totalRows,
        int totalPages,
        IReadOnlyList<Dictionary<string, object?>> data) =>
        new()
        {
            Type = type,
            Name = name,
            Page = page,
            PageSize = pageSize,
            TotalRows = totalRows,
            TotalPages = totalPages,
            Data = data
        };
}
