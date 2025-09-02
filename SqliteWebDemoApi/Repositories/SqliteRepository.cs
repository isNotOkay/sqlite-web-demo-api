using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SqliteWebDemoApi.Constants;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Options;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApi.Repositories;

public sealed class SqliteRepository(IOptions<DatabaseOptions> options) : ISqliteRepository
{
    private string ConnectionString => options.Value.Default;

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task<IReadOnlyList<SqliteRelationInfo>> ListRelationsAsync(string listSql, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var results = new List<SqliteRelationInfo>();

        await using var sqliteCommand = new SqliteCommand(listSql, connection);
        await using var reader = await sqliteCommand.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var quoted = SqliteIdentifierUtil.Quote(name);

            // Best-effort: some virtual objects/views may fail to count or enumerate schema
            var rowCount = await Try(async () => await CountRowsAsync(quoted, ct), fallback: 0L);
            var columns = await Try(async () => await GetColumnNamesAsync(quoted, ct), fallback: []);

            results.Add(new SqliteRelationInfo
            {
                Name = name,
                RowCount = rowCount,
                Columns = columns
            });
        }

        return results;
    }

    public async Task<bool> ObjectExistsAsync(string type, string name, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.ObjectExists, connection);
        sqliteCommand.Parameters.AddWithValue("@type", type);
        sqliteCommand.Parameters.AddWithValue("@name", name);
        var result = await sqliteCommand.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task<long> CountRowsAsync(string quotedName, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.CountAll(quotedName), connection);
        var result = await sqliteCommand.ExecuteScalarAsync(ct);
        return result is long longValue ? longValue : Convert.ToInt64(result ?? 0);
    }

    public async Task<string[]> GetColumnNamesAsync(string quotedName, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.SelectSchemaOnly(quotedName), connection);
        await using var reader = await sqliteCommand.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ct);

        var columns = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
            columns[i] = reader.GetName(i);
        return columns;
    }

    public async Task<bool> IsWithoutRowIdAsync(string tableName, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var sqliteCommand = new SqliteCommand(SqliteQueries.CheckWithoutRowId, connection);
        sqliteCommand.Parameters.AddWithValue("@name", tableName);
        var result = await sqliteCommand.ExecuteScalarAsync(ct);
        return result is long and > 0;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetPageAsync(
        string quotedName, bool orderByRowId, int take, int offset, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var sql = SqliteQueries.SelectPage(quotedName, orderByRowId);

        await using var sqliteCommand = new SqliteCommand(sql, connection);
        sqliteCommand.Parameters.AddWithValue("@take", take);
        sqliteCommand.Parameters.AddWithValue("@offset", offset);

        await using var reader = await sqliteCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

        var fieldCount = reader.FieldCount;
        var names = new string[fieldCount];
        for (var i = 0; i < fieldCount; i++) names[i] = reader.GetName(i);

        var rows = new List<Dictionary<string, object?>>(take);
        while (await reader.ReadAsync(ct))
            rows.Add(await ReadRowAsync(reader, names, ct));

        return rows;
    }

    private static async Task<T> Try<T>(Func<Task<T>> thunk, T fallback)
    {
        try { return await thunk(); }
        catch { return fallback; }
    }

    private static async Task<Dictionary<string, object?>> ReadRowAsync(
        SqliteDataReader reader, string[] names, CancellationToken ct)
    {
        var dictionary = new Dictionary<string, object?>(names.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < names.Length; i++)
        {
            var value = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            if (value is byte[] bytes) value = Convert.ToBase64String(bytes);
            dictionary[names[i]] = value;
        }
        return dictionary;
    }
}
