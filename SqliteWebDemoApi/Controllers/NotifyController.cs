using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SqliteWebDemoApi.Hubs;

namespace SqliteWebDemoApi.Controllers;

[ApiController]
[Route("api/notify")]
public sealed class NotifyController(IHubContext<NotificationsHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Send([FromQuery] string message = "Hello from server!")
    {
        await hub.Clients.All.SendAsync("notify", message);
        return Ok(new { ok = true, message });
    }

    // NEW: select a node (table or view) on all connected clients
    // GET /api/notify/select?kind=table&name=Orders
    [HttpGet("select")]
    public async Task<IActionResult> Select([FromQuery] string kind, [FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(name))
            return BadRequest("kind and name are required.");

        kind = kind.ToLowerInvariant();
        if (kind is not ("table" or "view"))
            return BadRequest("kind must be 'table' or 'view'.");

        await hub.Clients.All.SendAsync("selectNode", new { kind, id = name });
        return Ok(new { ok = true, kind, name });
    }
}