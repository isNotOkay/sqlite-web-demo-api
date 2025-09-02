using SqliteWebDemoApi.Models;

namespace SqliteWebDemoApi.Services;

public interface ISqliteService
{
    Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListTablesAsync(CancellationToken cancellationToken);
    Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListViewsAsync(CancellationToken cancellationToken);

    Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, string? sortBy, string? sortDir, CancellationToken cancellationToken);

    Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, string? sortBy, string? sortDir, CancellationToken cancellationToken);
}