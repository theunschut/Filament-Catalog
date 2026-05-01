using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OwnersController(IOwnerService ownerService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await ownerService.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create(OwnerCreateRequest req)
    {
        try
        {
            var owner = await ownerService.CreateAsync(req.Name);
            return Created($"/api/owners/{owner.Id}", owner);
        }
        catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await ownerService.DeleteAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
    }
}
