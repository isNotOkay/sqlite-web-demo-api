using System.Data;
using Microsoft.Data.Sqlite;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Repositories;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApi.Services;

public sealed class SqliteBrowser(ISqliteRepository sqliteRepository) : ISqliteBrowser
{
    public async Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListTablesAsync(CancellationToken cancellationToken) =>
        await ListRelationsAsync(SqliteQueries.ListTables, cancellationToken);

    public async Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListViewsAsync(CancellationToken cancellationToken) =>
        await ListRelationsAsync(SqliteQueries.ListViews, cancellationToken);

    public async Task<PagedResult<Dictionary<string, object?>>> GetTablePageAsync(
        string tableId, int page, int pageSize, CancellationToken cancellationToken)
    {
        SqliteIdentifiers.EnsureValid(tableId, nameof(tableId));

        await using var connection = await sqliteRepository.OpenConnectionAsync(cancellationToken);

        // Ensure table exists
        if (!await ObjectExistsAsync(connection, "table", tableId, cancellationToken))
            throw new KeyNotFoundException($"Table \"{tableId}\" not found.");

        var quoted = SqliteIdentifiers.Quote(tableId);
        var totalRows = await CountRowsAsync(connection, quoted, cancellationToken);

        // Paging
        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            Paginator.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildPage("table", tableId, normalizedPage, normalizedPageSize, 0, totalPages,
                []);

        // Use rowid ordering only if NOT WITHOUT ROWID
        var orderBy = await IsWithoutRowIdAsync(connection, tableId, cancellationToken) ? "" : "ORDER BY rowid";
        var dataSql = SqliteQueries.SelectPage(quoted, orderByRowId: orderBy != "");

        var rows = new List<Dictionary<string, object?>>(normalizedPageSize);
        await using (var sqliteCommand = new SqliteCommand(dataSql, connection))
        {
            sqliteCommand.Parameters.AddWithValue("@take", normalizedPageSize);
            sqliteCommand.Parameters.AddWithValue("@offset", offset);

            await using var reader = await sqliteCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

            while (await reader.ReadAsync(cancellationToken))
                rows.Add(await ReadRowAsync(reader, names, cancellationToken));
        }

        return BuildPage("table", tableId, normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    public async Task<PagedResult<Dictionary<string, object?>>> GetViewPageAsync(
        string viewId, int page, int pageSize, CancellationToken cancellationToken)
    {
        SqliteIdentifiers.EnsureValid(viewId, nameof(viewId));

        await using var connection = await sqliteRepository.OpenConnectionAsync(cancellationToken);

        // Ensure view exists
        if (!await ObjectExistsAsync(connection, "view", viewId, cancellationToken))
            throw new KeyNotFoundException($"View \"{viewId}\" not found.");

        var quoted = SqliteIdentifiers.Quote(viewId);
        var totalRows = await CountRowsAsync(connection, quoted, cancellationToken);

        // Paging
        var (normalizedPage, normalizedPageSize, totalPages, offset) =
            Paginator.Paginate(page, pageSize, totalRows);

        if (totalRows == 0)
            return BuildPage("view", viewId, normalizedPage, normalizedPageSize, 0, totalPages,
                []);

        var dataSql = SqliteQueries.SelectPage(quoted, orderByRowId: false);

        var rows = new List<Dictionary<string, object?>>(normalizedPageSize);
        await using (var sqliteCommand = new SqliteCommand(dataSql, connection))
        {
            sqliteCommand.Parameters.AddWithValue("@take", normalizedPageSize);
            sqliteCommand.Parameters.AddWithValue("@offset", offset);

            await using var reader = await sqliteCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            var fieldCount = reader.FieldCount;
            var names = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

            while (await reader.ReadAsync(cancellationToken))
                rows.Add(await ReadRowAsync(reader, names, cancellationToken));
        }

        return BuildPage("view", viewId, normalizedPage, normalizedPageSize, totalRows, totalPages, rows);
    }

    private async Task<(IReadOnlyList<SqliteRelationInfo> Items, int Total)> ListRelationsAsync(
        string listObjectsSql,
        CancellationToken cancellationToken)
    {
        await using var connection = await sqliteRepository.OpenConnectionAsync(cancellationToken);

        var results = new List<SqliteRelationInfo>();

        await using var command = new SqliteCommand(listObjectsSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var quoted = SqliteIdentifiers.Quote(name);

            var rowCount = await TryCountRowsAsync(connection, quoted, cancellationToken);
            var columns = await TryGetColumnsAsync(connection, quoted, cancellationToken);

            results.Add(new SqliteRelationInfo
            {
                Name = name,
                RowCount = rowCount,
                Columns = columns
            });
        }

        return (results, results.Count);
    }

    private static async Task<long> TryCountRowsAsync(SqliteConnection connection, string quoted, CancellationToken cancellationToken)
    {
        try { return await CountRowsAsync(connection, quoted, cancellationToken); }
        catch { return 0; }
    }

    private static async Task<string[]> TryGetColumnsAsync(SqliteConnection connection, string quoted, CancellationToken cancellationToken)
    {
        try { return await GetColumnNamesAsync(connection, quoted, cancellationToken); }
        catch { return []; }
    }
    
    private static async Task<string[]> GetColumnNamesAsync(SqliteConnection connection, string quotedName, CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.SelectSchemaOnly(quotedName), connection);
        await using var reader = await sqliteCommand.ExecuteReaderAsync(CommandBehavior.SchemaOnly, cancellationToken);
        for (var i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));
        return columns.ToArray();
    }

    private static async Task<Dictionary<string, object?>> ReadRowAsync(
        SqliteDataReader reader, string[] names, CancellationToken cancellationToken)
    {
        var dict = new Dictionary<string, object?>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < names.Length; i++)
        {
            var value = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
            if (value is byte[] bytes) value = Convert.ToBase64String(bytes);
            dict[names[i]] = value;
        }

        return dict;
    }

    private static async Task<bool> ObjectExistsAsync(SqliteConnection connection, string type, string name, CancellationToken cancellationToken)
    {
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.ObjectExists, connection);
        sqliteCommand.Parameters.AddWithValue("@type", type);
        sqliteCommand.Parameters.AddWithValue("@name", name);
        var exists = await sqliteCommand.ExecuteScalarAsync(cancellationToken);
        return exists is not null;
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string quotedName, CancellationToken cancellationToken)
    {
        var sql = SqliteQueries.CountAll(quotedName);
        await using var sqliteCommand = new SqliteCommand(sql, connection);
        var value = await sqliteCommand.ExecuteScalarAsync(cancellationToken);
        return (long)(value ?? 0L);
    }

    private static async Task<bool> IsWithoutRowIdAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.CheckWithoutRowId, connection);
        sqliteCommand.Parameters.AddWithValue("@name", tableName);
        var value = await sqliteCommand.ExecuteScalarAsync(cancellationToken);
        return value is long and > 0;
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
