using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "catalog-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "FilamentCatalog";
    });

    var dbPath = Path.Combine(AppContext.BaseDirectory, "filament.db");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    builder.Services.ConfigureHttpJsonOptions(o =>
        o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ClearStaleEfMigrationsLock(db);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    app.UseDefaultFiles();   // MUST be before UseStaticFiles — per CLAUDE.md
    app.UseStaticFiles();

    // ---- Owners ----
    app.MapGet("/api/owners", async (AppDbContext db) =>
        await db.Owners.OrderBy(o => o.CreatedAt).ToListAsync());

    app.MapPost("/api/owners", async (AppDbContext db, OwnerCreateRequest req) =>
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.UnprocessableEntity(new { error = "Name is required." });
        var owner = new Owner { Name = req.Name.Trim(), IsMe = false, CreatedAt = DateTime.UtcNow };
        db.Owners.Add(owner);
        await db.SaveChangesAsync();
        return Results.Created($"/api/owners/{owner.Id}", owner);
    });

    app.MapDelete("/api/owners/{id:int}", async (AppDbContext db, int id) =>
    {
        var owner = await db.Owners.FindAsync(id);
        if (owner is null) return Results.NotFound(new { error = "Owner not found." });
        if (owner.IsMe) return Results.UnprocessableEntity(new { error = "Cannot delete the 'Me' owner." });
        var spoolCount = await db.Spools.CountAsync(s => s.OwnerId == id);
        if (spoolCount > 0)
            return Results.Conflict(new { error = $"Cannot delete — {spoolCount} spool(s) assigned. Remove spools first." });
        db.Owners.Remove(owner);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task ClearStaleEfMigrationsLock(AppDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsLock\"");
    }
    catch
    {
        // Table doesn't exist on first run — this is expected and safe to ignore
    }
}

static async Task SeedAsync(AppDbContext db)
{
    if (!await db.Owners.AnyAsync())
    {
        db.Owners.Add(new Owner { Name = "Me", IsMe = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}

record OwnerCreateRequest(string Name);
