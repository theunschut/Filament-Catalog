using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SummaryController(ISummaryService summaryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSummary() =>
        Ok(await summaryService.GetSummaryAsync());
}
