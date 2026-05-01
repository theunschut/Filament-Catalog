using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class BalanceController(ISummaryService summaryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBalance() =>
        Ok(await summaryService.GetBalanceAsync());
}
