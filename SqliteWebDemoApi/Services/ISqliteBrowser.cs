using Models;
using SqliteWebDemoApi.Models;

namespace SqliteWebDemoApi.Services;

public interface ISqliteBrowser
{
    Task<(IReadOnlyList<TableInfo> Items, int Total)> ListTablesAsync(CancellationToken ct);
    Task<(IReadOnlyList<ViewInfo> Items, int Total)> ListViewsAsync(CancellationToken ct);

    Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, CancellationToken ct);

    Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, CancellationToken cancellationToken);
}