using Microsoft.AspNetCore.Mvc;
using SqliteWebDemoApi.Services;

namespace SqliteWebDemoApi.Controllers;

[ApiController]
[Route("api")]
public sealed class DatabaseController(ISqliteBrowser browser) : ControllerBase
{
    // ---------- GET /api/tables ----------
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables(CancellationToken ct)
    {
        var (items, total) = await browser.ListTablesAsync(ct);
        return Ok(new { items, total });
    }

    // ---------- GET /api/views ----------
    [HttpGet("views")]
    public async Task<IActionResult> GetViews(CancellationToken ct)
    {
        var (items, total) = await browser.ListViewsAsync(ct);
        return Ok(new { items, total });
    }

    // ---------- GET /api/tables/{tableId} ----------
    [HttpGet("tables/{tableId}")]
    public async Task<IActionResult> GetTableData(
        string tableId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var result = await browser.GetTablePageAsync(tableId, page, pageSize, ct);
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

    // ---------- GET /api/views/{viewId} ----------
    [HttpGet("views/{viewId}")]
    public async Task<IActionResult> GetViewData(
        string viewId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var result = await browser.GetViewPageAsync(viewId, page, pageSize, ct);
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