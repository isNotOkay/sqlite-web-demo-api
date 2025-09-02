namespace SqliteWebDemoApi.Models;

public sealed class TableInfo
{
    public required string Name { get; init; }
    public required long RowCount { get; init; }
    public required IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
}