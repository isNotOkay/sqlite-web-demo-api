namespace SqliteWebDemoApi.Models;

public sealed class SqliteRelationInfo
{
    public required string Name { get; init; }
    public required long RowCount { get; init; }
    public required IReadOnlyList<string> Columns { get; init; } = [];
}