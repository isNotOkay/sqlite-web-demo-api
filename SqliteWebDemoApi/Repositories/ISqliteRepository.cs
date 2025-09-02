namespace SqliteWebDemoApi.Repositories;

using Microsoft.Data.Sqlite;

public interface ISqliteRepository
{
    Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct);
}