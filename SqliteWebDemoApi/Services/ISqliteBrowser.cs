using SqliteWebDemoApi.Models;

namespace SqliteWebDemoApi.Services;

public interface ISqliteBrowser
{
    Task<(IReadOnlyList<DbObjectInfo> Items, int Total)> ListTablesAsync(CancellationToken cancellationToken);
    Task<(IReadOnlyList<DbObjectInfo> Items, int Total)> ListViewsAsync(CancellationToken cancellationToken);

    Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, CancellationToken cancellationToken);
}