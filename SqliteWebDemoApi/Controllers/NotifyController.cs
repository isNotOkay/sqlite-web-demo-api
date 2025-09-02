using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SqliteWebDemoApi.Hubs;

namespace SqliteWebDemoApi.Controllers;

[ApiController]
[Route("api/notify")]
public sealed class NotifyController(IHubContext<NotificationsHub> hub) : ControllerBase
{
    // POST /api/notify?message=Hello
    [HttpPost]
    public async Task<IActionResult> Send([FromQuery] string message = "Hello from server!")
    {
        await hub.Clients.All.SendAsync("notify", message);
        return Ok(new { ok = true, message });
    }

    // Handy GET for quick testing in the browser
    [HttpGet]
    public async Task<IActionResult> SendGet([FromQuery] string message = "Hello from server!")
    {
        await hub.Clients.All.SendAsync("notify", message);
        return Ok(new { ok = true, message });
    }
}