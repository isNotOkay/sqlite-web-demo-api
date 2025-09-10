using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SqliteWebDemoApi.Models;
using SqliteWebDemoApi.Services;

namespace SqliteWebDemoApi.Controllers;

[ApiController]
[Route("api")]
public sealed class SqliteController(ISqliteService service) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;

    [HttpGet("tables")]
    public async Task<ActionResult<PageResult<SqliteRelationInfo>>> GetTables(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? sortBy = "Name",
        [FromQuery] string? sortDir = "asc",
        CancellationToken ct = default)
    {
        var (items, total) = await service.ListTablesAsync(ct);
        return Ok(BuildPageResult(items, total, page, pageSize, sortBy, sortDir));
    }

    [HttpGet("views")]
    public async Task<ActionResult<PageResult<SqliteRelationInfo>>> GetViews(
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? sortBy = "Name",
        [FromQuery] string? sortDir = "asc",
        CancellationToken ct = default)
    {
        var (items, total) = await service.ListViewsAsync(ct);
        return Ok(BuildPageResult(items, total, page, pageSize, sortBy, sortDir));
    }

    [HttpGet("tables/{tableId}")]
    public async Task<IActionResult> GetTableData(
        string tableId,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = "asc",
        CancellationToken ct = default)
    {
        try
        {
            // Assumes service updated to return PageResult<T>
            var result = await service.GetTablePageAsync(tableId, page, pageSize, sortBy, sortDir, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("views/{viewId}")]
    public async Task<IActionResult> GetViewData(
        string viewId,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = "asc",
        CancellationToken ct = default)
    {
        try
        {
            // Assumes service updated to return PageResult<T>
            var result = await service.GetViewPageAsync(viewId, page, pageSize, sortBy, sortDir, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    private static PageResult<SqliteRelationInfo> BuildPageResult(
        IReadOnlyList<SqliteRelationInfo> source,
        long total,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDir)
    {
        pageSize = pageSize <= 0 ? DefaultPageSize : pageSize;

        IEnumerable<SqliteRelationInfo> query = source;

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var prop = typeof(SqliteRelationInfo).GetProperty(
                sortBy,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is not null)
            {
                bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
                query = desc
                    ? query.OrderByDescending(x => prop.GetValue(x, null))
                    : query.OrderBy(x => prop.GetValue(x, null));
            }
        }

        var totalPages = (int)Math.Max(1, Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);

        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PageResult<SqliteRelationInfo>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            Items = items
        };
    }
}
