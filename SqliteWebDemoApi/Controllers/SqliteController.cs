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
    public async Task<ActionResult<ListResponse<SqliteRelationInfo>>> GetTables(CancellationToken ct)
    {
        var (items, total) = await service.ListTablesAsync(ct);
        return Ok(new ListResponse<SqliteRelationInfo> { Items = items, Total = total });
    }

    [HttpGet("views")]
    public async Task<ActionResult<ListResponse<SqliteRelationInfo>>> GetViews(CancellationToken ct)
    {
        var (items, total) = await service.ListViewsAsync(ct);
        return Ok(new ListResponse<SqliteRelationInfo> { Items = items, Total = total });
    }

    [HttpGet("tables/{tableId}")]
    public async Task<IActionResult> GetTableData(
        string tableId,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        try
        {
            var result = await service.GetTablePageAsync(tableId, page, pageSize, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("views/{viewId}")]
    public async Task<IActionResult> GetViewData(
        string viewId,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        try
        {
            var result = await service.GetViewPageAsync(viewId, page, pageSize, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}