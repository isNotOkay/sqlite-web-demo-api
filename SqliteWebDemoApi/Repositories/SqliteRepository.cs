using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Options;
using SqliteWebDemoApi.Utilities;

namespace SqliteWebDemoApi.Repositories;

public sealed class SqliteRepository(IOptions<DatabaseOptions> options) : ISqliteRepository
{
    private string ConnectionString => options.Value.Default;

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<IReadOnlyList<SqliteRelationInfo>> ListRelationsAsync(string listSql, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var results = new List<SqliteRelationInfo>();

        await using var command = new SqliteCommand(listSql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var quoted = SqliteIdentifiers.Quote(name);

            // Best-effort: some virtual objects/views may fail to count or enumerate schema
            var rowCount = await Try(async () => await CountRowsAsync(quoted, ct), fallback: 0L);
            var columns  = await Try(async () => await GetColumnNamesAsync(quoted, ct), fallback: []);

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
        await using var cmd = new SqliteCommand(SqliteQueries.ObjectExists, connection);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@name", name);
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj is not null;
    }

    public async Task<long> CountRowsAsync(string quotedName, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var cmd = new SqliteCommand(SqliteQueries.CountAll(quotedName), connection);
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj is long l ? l : Convert.ToInt64(obj ?? 0);
    }

    public async Task<string[]> GetColumnNamesAsync(string quotedName, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var cmd = new SqliteCommand(SqliteQueries.SelectSchemaOnly(quotedName), connection);
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ct);

        var cols = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
            cols[i] = reader.GetName(i);
        return cols;
    }

    public async Task<bool> IsWithoutRowIdAsync(string tableName, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var cmd = new SqliteCommand(SqliteQueries.CheckWithoutRowId, connection);
        cmd.Parameters.AddWithValue("@name", tableName);
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj is long n && n > 0;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetPageAsync(
        string quotedName, bool orderByRowId, int take, int offset, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var sql = SqliteQueries.SelectPage(quotedName, orderByRowId);

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@take", take);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

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
        var dict = new Dictionary<string, object?>(names.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < names.Length; i++)
        {
            var value = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            if (value is byte[] bytes) value = Convert.ToBase64String(bytes);
            dict[names[i]] = value;
        }
        return dict;
    }
}
