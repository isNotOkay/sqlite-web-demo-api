using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SqliteWebDemoApi.Options;

namespace SqliteWebDemoApi.Repositories;

public sealed class SqliteRepository(IOptions<DatabaseOptions> options) : ISqliteRepository
{
    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connectionString = options.Value.Default; 
        var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}