// Replaces the “give me a connection” style with focused operations.

using SqliteWebDemoApi.Models;

namespace SqliteWebDemoApi.Repositories;

public interface ISqliteRepository
{
    Task<IReadOnlyList<SqliteRelationInfo>> ListRelationsAsync(string listSql, CancellationToken ct);
    Task<bool> ObjectExistsAsync(string type, string name, CancellationToken ct);
    Task<long> CountRowsAsync(string quotedName, CancellationToken ct);
    Task<bool> IsWithoutRowIdAsync(string tableName, CancellationToken ct);

    Task<IReadOnlyList<Dictionary<string, object?>>> GetPageAsync(
        string quotedName, bool orderByRowId, int take, int offset, CancellationToken ct);
}