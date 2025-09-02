using Microsoft.Data.Sqlite;

namespace SqliteWebDemoApi.Repositories;

public sealed class SqliteRepository(string connectionString) : ISqliteRepository
{
    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}