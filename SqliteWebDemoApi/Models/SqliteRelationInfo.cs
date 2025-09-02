namespace SqliteWebDemoApi.Models;

/// <summary>
/// Represents a relational object in SQLite.
/// This can be either a <b>table</b> or a <b>view</b>.
/// </summary>
public sealed class SqliteRelationInfo
{
    public required string Name { get; init; }
    public required long RowCount { get; init; }
    public required IReadOnlyList<string> Columns { get; init; } = [];
}