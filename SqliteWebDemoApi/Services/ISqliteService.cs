using SqliteWebDemoApi.Models;

namespace SqliteWebDemoApi.Services;

public interface ISqliteService
{
    Task<(IReadOnlyList<SqliteRelationInfo> Items, long Total)> ListTablesAsync(CancellationToken cancellationToken);
    Task<(IReadOnlyList<SqliteRelationInfo> Items, long Total)> ListViewsAsync(CancellationToken cancellationToken);

    Task<PageResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken);

    Task<PageResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken);
}