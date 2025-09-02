namespace SqliteWebDemoApi.Models;

public sealed class ListResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }
}