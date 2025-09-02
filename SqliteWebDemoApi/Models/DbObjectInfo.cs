namespace SqliteWebDemoApi.Models;

public sealed class DbObjectInfo
{
    public required string Name { get; init; }
    public required long RowCount { get; init; }
    public required IReadOnlyList<string> Columns { get; init; } = [];
}