namespace Models;

public sealed class ViewInfo
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
}