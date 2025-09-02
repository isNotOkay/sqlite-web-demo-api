namespace SqliteWebDemoApi.Models;

public sealed class PagedResult<T>
{
    public required string Type { get; init; }  // "table" | "view"
    public required string Name { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required long TotalRows { get; init; }
    public required int TotalPages { get; init; }
    public required IReadOnlyList<T> Data { get; init; } = Array.Empty<T>();
}