using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/catalog")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    // GET /api/catalog/count — returns { count: N }
    // Used by JS to decide whether to disable the Add Spool button (per CONTEXT.md D-05)
    [HttpGet("count")]
    public async Task<IActionResult> GetCount()
    {
        var count = await db.BambuProducts.CountAsync();
        return Ok(new { count });
    }

    // GET /api/catalog/materials — returns ["ABS", "PETG", "PLA", ...]
    // Distinct material values, sorted alphabetically for the material <select>
    [HttpGet("materials")]
    public async Task<IActionResult> GetMaterials()
    {
        var materials = await db.BambuProducts
            .Select(p => p.Material)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();
        return Ok(materials);
    }

    // GET /api/catalog/colors?material=PLA — returns color variant objects for a material
    // Response shape: [{ id, colorName, colorHex, productTitle }]
    // colorName and productTitle are used by catalog.js to auto-fill spool Name: "${productTitle} — ${colorName}"
    [HttpGet("colors")]
    public async Task<IActionResult> GetColors([FromQuery] string material)
    {
        if (string.IsNullOrWhiteSpace(material))
            return BadRequest(new { error = "material query parameter is required" });

        var colors = await db.BambuProducts
            .Where(p => p.Material == material)
            .OrderBy(p => p.ColorName)
            .Select(p => new
            {
                id = p.Id,
                colorName = p.ColorName,
                colorHex = p.ColorHex,
                productTitle = p.Material
            })
            .ToListAsync();

        return Ok(colors);
    }
}
