using Microsoft.AspNetCore.Mvc;
using System.Threading.Channels;

[ApiController]
[Route("api/sync")]
public class SyncController(Channel<SyncJob> channel, SyncStateService stateService) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartSync()
    {
        var job = new SyncJob(Id: Environment.TickCount);
        // TryWrite instead of WriteAsync — channel has capacity 1 with DropNewest;
        // if already full the old job is dropped and the new one is written.
        // WriteAsync would wait (but with DropNewest the write completes immediately anyway).
        await channel.Writer.WriteAsync(job);
        return Accepted(new { message = "Sync started" });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(stateService.GetStatus());
    }
}
