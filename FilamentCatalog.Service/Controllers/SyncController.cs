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
        // WriteAsync on a BoundedChannel with DropNewest completes immediately —
        // if the channel is full the oldest item is dropped and the new one is written.
        await channel.Writer.WriteAsync(job);
        return Accepted(new { message = "Sync started" });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(stateService.GetStatus());
    }
}
