using SqliteWebDemoApi.Models;

namespace SqliteWebDemoApi.Repositories;

public interface ISqliteRepository
{
    Task<IReadOnlyList<SqliteRelationInfo>> ListRelationsAsync(string listSql, CancellationToken ct);
    Task<bool> ObjectExistsAsync(string type, string name, CancellationToken ct);
    Task<long> CountRowsAsync(string quotedName, CancellationToken ct);
    Task<bool> IsWithoutRowIdAsync(string tableName, CancellationToken ct);

    // NEW: expose schema to validate sortBy
    Task<string[]> GetColumnNamesAsync(string quotedName, CancellationToken ct);

    // UPDATED: allow ordered pagination
    Task<IReadOnlyList<Dictionary<string, object?>>> GetPageAsync(
        string quotedName,
        string? orderByColumn,
        bool orderByDesc,
        bool addRowIdTiebreaker,
        int take,
        int offset,
        CancellationToken ct);
}