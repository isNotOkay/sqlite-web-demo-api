using System.Data;
using Microsoft.Data.Sqlite;
using Models;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApi.Services;

public sealed class SqliteBrowser(ISqliteRepository repo) : ISqliteBrowser
{
    private static async Task<string[]> GetColumnNamesAsync(SqliteConnection conn, string quotedName, CancellationToken ct)
    {
        var cols = new List<string>();
        await using var cmd = new SqliteCommand(SqliteQueries.SelectSchemaOnly(quotedName), conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ct);
        for (var i = 0; i < reader.FieldCount; i++)
            cols.Add(reader.GetName(i));
        return cols.ToArray();
    }

    // ---------- LIST: /api/tables ----------
    public async Task<(IReadOnlyList<TableInfo> Items, int Total)> ListTablesAsync(CancellationToken ct)
    {
        await using var conn = await repo.OpenConnectionAsync(ct);

        var results = new List<TableInfo>();

        await using (var cmd = new SqliteCommand(SqliteQueries.ListTables, conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var name = r.GetString(0);
                var quoted = SqliteIdentifiers.Quote(name);

                // Row count (best-effort)
                long rowCount = 0;
                try
                {
                    rowCount = await CountRowsAsync(conn, quoted, ct);
                }
                catch
                {
                    // virtual tables / views may throw; ignore for summary
                }

                // Column names
                string[] columns;
                try { columns = await GetColumnNamesAsync(conn, quoted, ct); }
                catch { columns = []; }

                results.Add(new TableInfo
                {
                    Name = name,
                    RowCount = rowCount,
                    Columns = columns
                });
            }
        }

        return (results, results.Count);
    }

    // ---------- LIST: /api/views ----------
    public async Task<(IReadOnlyList<ViewInfo> Items, int Total)> ListViewsAsync(CancellationToken ct)
    {
        await using var conn = await repo.OpenConnectionAsync(ct);

        var results = new List<ViewInfo>();

        await using (var cmd = new SqliteCommand(SqliteQueries.ListViews, conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var name = r.GetString(0);
                var quoted = SqliteIdentifiers.Quote(name);

                string[] columns;
                try { columns = await GetColumnNamesAsync(conn, quoted, ct); }
                catch { columns = []; }

                results.Add(new ViewInfo
                {
                    Name = name,
                    Columns = columns
                });
            }
        }

        return (results, results.Count);
    }

    // ---------- DATA: /api/tables/{tableId} ----------
    public async Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, CancellationToken ct)
    {
        SqliteIdentifiers.EnsureValid(tableId, nameof(tableId));

        await using var conn = await repo.OpenConnectionAsync(ct);

        // Ensure table exists
        if (!await ObjectExistsAsync(conn, "table", tableId, ct))
            throw new KeyNotFoundException($"Table \"{tableId}\" not found.");

        var quoted = SqliteIdentifiers.Quote(tableId);
        var totalRows = await CountRowsAsync(conn, quoted, ct);

        // Paging
        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            Paginator.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildEmptyPage("table", tableId, normalizedPage, normalizedPageSize, totalPages);

        // Use rowid ordering only if NOT WITHOUT ROWID
        var orderBy = await IsWithoutRowIdAsync(conn, tableId, ct) ? "" : "ORDER BY rowid";
        var dataSql = SqliteQueries.SelectPage(quoted, orderByRowId: orderBy != "");

        var rows = new List<Dictionary<string, object?>>(normalizedPageSize);
        await using (var cmd = new SqliteCommand(dataSql, conn))
        {
            cmd.Parameters.AddWithValue("@take", normalizedPageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

            while (await reader.ReadAsync(ct))
                rows.Add(await ReadRowAsync(reader, names, ct));
        }

        return BuildPage("table", tableId, normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    // ---------- DATA: /api/views/{viewId} ----------
    public async Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, CancellationToken ct)
    {
        SqliteIdentifiers.EnsureValid(viewId, nameof(viewId));

        await using var conn = await repo.OpenConnectionAsync(ct);

        // Ensure view exists
        if (!await ObjectExistsAsync(conn, "view", viewId, ct))
            throw new KeyNotFoundException($"View \"{viewId}\" not found.");

        var quoted = SqliteIdentifiers.Quote(viewId);
        var totalRows = await CountRowsAsync(conn, quoted, ct);

        // Paging
        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            Paginator.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildEmptyPage("view", viewId, normalizedPage, normalizedPageSize, totalPages);

        var dataSql = SqliteQueries.SelectPage(quoted, orderByRowId: false);

        var rows = new List<Dictionary<string, object?>>(normalizedPageSize);
        await using (var cmd = new SqliteCommand(dataSql, conn))
        {
            cmd.Parameters.AddWithValue("@take", normalizedPageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

            while (await reader.ReadAsync(ct))
                rows.Add(await ReadRowAsync(reader, names, ct));
        }

        return BuildPage("view", viewId, normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    // ---------- Helpers ----------
    private static async Task<Dictionary<string, object?>> ReadRowAsync(
        SqliteDataReader reader, string[] names, CancellationToken ct)
    {
        var dict = new Dictionary<string, object?>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < names.Length; i++)
        {
            var val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            if (val is byte[] bytes) val = Convert.ToBase64String(bytes);
            dict[names[i]] = val;
        }

        return dict;
    }

    private static async Task<bool> ObjectExistsAsync(SqliteConnection conn, string type, string name, CancellationToken ct)
    {
        await using var cmd = new SqliteCommand(SqliteQueries.ObjectExists, conn);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@name", name);
        var exists = await cmd.ExecuteScalarAsync(ct);
        return exists is not null;
    }

    private static async Task<long> CountRowsAsync(SqliteConnection conn, string quotedName, CancellationToken ct)
    {
        var sql = SqliteQueries.CountAll(quotedName);
        await using var cmd = new SqliteCommand(sql, conn);
        var v = await cmd.ExecuteScalarAsync(ct);
        return (long)(v ?? 0L);
    }

    private static async Task<bool> IsWithoutRowIdAsync(SqliteConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = new SqliteCommand(SqliteQueries.CheckWithoutRowId, conn);
        cmd.Parameters.AddWithValue("@name", tableName);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is long n && n > 0;
    }

    private static PagedResult<Dictionary<string, object?>> BuildEmptyPage(string type, string name, int page, int pageSize, int totalPages) =>
        new()
        {
            Type = type,
            Name = name,
            Page = page,
            PageSize = pageSize,
            TotalRows = 0,
            TotalPages = totalPages,
            Data = []
        };

    private static PagedResult<Dictionary<string, object?>> BuildPage(string type, string name, int page, int pageSize, long totalRows, int totalPages, List<Dictionary<string, object?>> rows) =>
        new()
        {
            Type = type,
            Name = name,
            Page = page,
            PageSize = pageSize,
            TotalRows = totalRows,
            TotalPages = totalPages,
            Data = rows
        };
}
