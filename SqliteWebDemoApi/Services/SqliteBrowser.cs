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
        await using var cmd = new SqliteCommand($"SELECT * FROM {quotedName} LIMIT 0;", conn);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ct);
        for (var i = 0; i < reader.FieldCount; i++)
            cols.Add(reader.GetName(i));
        return cols.ToArray();
    }

    // ---------- LIST: tables ----------
    public async Task<(IReadOnlyList<TableInfo> Items, int Total)> ListTablesAsync(CancellationToken ct)
    {
        await using var conn = await repo.OpenConnectionAsync(ct);

        var results = new List<TableInfo>();

        const string tablesSql = @"
SELECT name, sql
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%'
ORDER BY name;";

        await using (var cmd = new SqliteCommand(tablesSql, conn))
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
                    await using var c2 = new SqliteCommand($"SELECT COUNT(*) FROM {quoted};", conn);
                    rowCount = (long)(await c2.ExecuteScalarAsync(ct) ?? 0L);
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

    // ---------- LIST: views ----------
    public async Task<(IReadOnlyList<ViewInfo> Items, int Total)> ListViewsAsync(CancellationToken ct)
    {
        await using var conn = await repo.OpenConnectionAsync(ct);

        var results = new List<ViewInfo>();

        const string viewsSql = @"
SELECT name, sql
FROM sqlite_master
WHERE type = 'view'
ORDER BY name;";

        await using (var cmd = new SqliteCommand(viewsSql, conn))
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

    // ---------- DATA: tables/{tableId} ----------
    public async Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, CancellationToken ct)
    {
        SqliteIdentifiers.EnsureValid(tableId, nameof(tableId));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        await using var conn = await repo.OpenConnectionAsync(ct);

        // Ensure table exists
        const string tableExistsSql = @"SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name;";
        await using (var existsCmd = new SqliteCommand(tableExistsSql, conn))
        {
            existsCmd.Parameters.AddWithValue("@name", tableId);
            var exists = await existsCmd.ExecuteScalarAsync(ct);
            if (exists is null) throw new KeyNotFoundException($"Table \"{tableId}\" not found.");
        }

        // Count rows
        var quoted = SqliteIdentifiers.Quote(tableId);
        long totalRows;
        await using (var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM {quoted};", conn))
            totalRows = (long)(await countCmd.ExecuteScalarAsync(ct) ?? 0L);

        var totalPages = (int)Math.Max(1, Math.Ceiling(totalRows / (double)pageSize));
        if (totalRows == 0)
        {
            return new PagedResult<Dictionary<string, object?>>
            {
                Type = "table",
                Name = tableId,
                Page = page,
                PageSize = pageSize,
                TotalRows = 0,
                TotalPages = totalPages,
                Data = []
            };
        }

        page = Math.Min(page, totalPages);
        var offset = (page - 1) * pageSize;

        // Use rowid ordering only if NOT WITHOUT ROWID
        var withoutRowId = false;
        const string checkWithoutRowId = @"SELECT instr(lower(sql),'without rowid') FROM sqlite_master WHERE type='table' AND name=@name;";
        await using (var chk = new SqliteCommand(checkWithoutRowId, conn))
        {
            chk.Parameters.AddWithValue("@name", tableId);
            var v = await chk.ExecuteScalarAsync(ct);
            withoutRowId = (v is long n && n > 0);
        }
        var orderBy = withoutRowId ? "" : "ORDER BY rowid";

        var dataSql = $@"
SELECT *
FROM {quoted}
{orderBy}
LIMIT @take OFFSET @offset;";

        var rows = new List<Dictionary<string, object?>>(pageSize);
        await using (var cmd = new SqliteCommand(dataSql, conn))
        {
            cmd.Parameters.AddWithValue("@take", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

            while (await reader.ReadAsync(ct))
                rows.Add(await ReadRowAsync(reader, names, ct));
        }

        return new PagedResult<Dictionary<string, object?>>
        {
            Type = "table",
            Name = tableId,
            Page = page,
            PageSize = pageSize,
            TotalRows = totalRows,
            TotalPages = totalPages,
            Data = rows
        };
    }

    // ---------- DATA: views/{viewId} ----------
    public async Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, CancellationToken cancellationToken)
    {
        SqliteIdentifiers.EnsureValid(viewId, nameof(viewId));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        await using var connection = await repo.OpenConnectionAsync(cancellationToken);

        // Ensure view exists
        const string viewExistsSql = @"SELECT 1 FROM sqlite_master WHERE type='view' AND name=@name;";
        await using (var existsCmd = new SqliteCommand(viewExistsSql, connection))
        {
            existsCmd.Parameters.AddWithValue("@name", viewId);
            var exists = await existsCmd.ExecuteScalarAsync(cancellationToken);
            if (exists is null) throw new KeyNotFoundException($"View \"{viewId}\" not found.");
        }

        var quoted = SqliteIdentifiers.Quote(viewId);

        long totalRows;
        await using (var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM {quoted};", connection))
            totalRows = (long)(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);

        var totalPages = (int)Math.Max(1, Math.Ceiling(totalRows / (double)pageSize));
        if (totalRows == 0)
        {
            return new PagedResult<Dictionary<string, object?>>
            {
                Type = "view",
                Name = viewId,
                Page = page,
                PageSize = pageSize,
                TotalRows = 0,
                TotalPages = totalPages,
                Data = []
            };
        }

        page = Math.Min(page, totalPages);
        var offset = (page - 1) * pageSize;

        var dataSql = $@"
SELECT *
FROM {quoted}
LIMIT @take OFFSET @offset;";

        var rows = new List<Dictionary<string, object?>>(pageSize);
        await using (var cmd = new SqliteCommand(dataSql, connection))
        {
            cmd.Parameters.AddWithValue("@take", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

            while (await reader.ReadAsync(cancellationToken))
                rows.Add(await ReadRowAsync(reader, names, cancellationToken));
        }

        return new PagedResult<Dictionary<string, object?>>
        {
            Type = "view",
            Name = viewId,
            Page = page,
            PageSize = pageSize,
            TotalRows = totalRows,
            TotalPages = totalPages,
            Data = rows
        };
    }
    
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
}
