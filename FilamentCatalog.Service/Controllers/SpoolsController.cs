using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SpoolsController(ISpoolService spoolService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await spoolService.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create(SpoolCreateRequest req)
    {
        try
        {
            var spool = await spoolService.CreateAsync(req);
            return Created($"/api/spools/{spool.Id}", spool);
        }
        catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SpoolUpdateRequest req)
    {
        try
        {
            var spool = await spoolService.UpdateAsync(id, req);
            return Ok(spool);
        }
        catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await spoolService.DeleteAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
