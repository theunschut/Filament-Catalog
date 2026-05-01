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

    // ---- Spools ----
    app.MapGet("/api/spools", async (AppDbContext db) =>
        await db.Spools.Include(s => s.Owner).OrderBy(s => s.CreatedAt).ToListAsync());

    app.MapPost("/api/spools", async (AppDbContext db, SpoolCreateRequest req) =>
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.UnprocessableEntity(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Material))
            return Results.UnprocessableEntity(new { error = "Material is required." });
        var ownerExists = await db.Owners.AnyAsync(o => o.Id == req.OwnerId);
        if (!ownerExists) return Results.UnprocessableEntity(new { error = "Owner not found." });

        var colorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? "#888888" : req.ColorHex;
        var spool = new Spool
        {
            Name = req.Name.Trim(),
            Material = req.Material.Trim(),
            ColorHex = colorHex,
            OwnerId = req.OwnerId,
            WeightGrams = req.WeightGrams,
            PricePaid = req.PricePaid,
            PaymentStatus = req.PaymentStatus,
            SpoolStatus = req.SpoolStatus,
            Notes = req.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.Spools.Add(spool);
        await db.SaveChangesAsync();
        return Results.Created($"/api/spools/{spool.Id}", spool);
    });

    app.MapPut("/api/spools/{id:int}", async (AppDbContext db, int id, SpoolUpdateRequest req) =>
    {
        var spool = await db.Spools.FindAsync(id);
        if (spool is null) return Results.NotFound(new { error = "Spool not found." });
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.UnprocessableEntity(new { error = "Name is required." });
        if (string.IsNullOrWhiteSpace(req.Material))
            return Results.UnprocessableEntity(new { error = "Material is required." });
        var ownerExists = await db.Owners.AnyAsync(o => o.Id == req.OwnerId);
        if (!ownerExists) return Results.UnprocessableEntity(new { error = "Owner not found." });

        spool.Name = req.Name.Trim();
        spool.Material = req.Material.Trim();
        spool.ColorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? "#888888" : req.ColorHex;
        spool.OwnerId = req.OwnerId;
        spool.WeightGrams = req.WeightGrams;
        spool.PricePaid = req.PricePaid;
        spool.PaymentStatus = req.PaymentStatus;
        spool.SpoolStatus = req.SpoolStatus;
        spool.Notes = req.Notes?.Trim();
        await db.SaveChangesAsync();
        return Results.Ok(spool);
    });

    app.MapDelete("/api/spools/{id:int}", async (AppDbContext db, int id) =>
    {
        var spool = await db.Spools.FindAsync(id);
        if (spool is null) return Results.NotFound(new { error = "Spool not found." });
        db.Spools.Remove(spool);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });

    // ---- Summary ----
    app.MapGet("/api/summary", async (AppDbContext db) =>
    {
        var meOwner = await db.Owners.FirstOrDefaultAsync(o => o.IsMe);
        var spools = await db.Spools.ToListAsync();
        var mySpoolCount = meOwner is null ? 0 : spools.Count(s => s.OwnerId == meOwner.Id);
        var totalValue = spools.Where(s => s.PricePaid.HasValue).Sum(s => s.PricePaid!.Value);
        var totalOwed = spools
            .Where(s => meOwner is not null && s.OwnerId != meOwner.Id
                        && s.PaymentStatus != PaymentStatus.Paid
                        && s.PricePaid.HasValue)
            .Sum(s => s.PricePaid!.Value);
        return Results.Ok(new { totalSpools = spools.Count, mySpools = mySpoolCount, totalValue, totalOwed });
    });

    // ---- Balance ----
    app.MapGet("/api/balance", async (AppDbContext db) =>
    {
        var nonMeOwners = await db.Owners.Where(o => !o.IsMe).OrderBy(o => o.Name).ToListAsync();
        var allSpools = await db.Spools.ToListAsync();
        var rows = nonMeOwners.Select(owner =>
        {
            var ownerSpools = allSpools.Where(s => s.OwnerId == owner.Id).ToList();
            var value = ownerSpools.Where(s => s.PricePaid.HasValue).Sum(s => s.PricePaid!.Value);
            var owed = ownerSpools
                .Where(s => s.PaymentStatus != PaymentStatus.Paid && s.PricePaid.HasValue)
                .Sum(s => s.PricePaid!.Value);
            var hasUnpriced = ownerSpools.Any(s => !s.PricePaid.HasValue);
            return new { ownerId = owner.Id, ownerName = owner.Name, spoolCount = ownerSpools.Count, value, owed, hasUnpriced };
        });
        return Results.Ok(rows);
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

record SpoolCreateRequest(
    string Name, string Material, string? ColorHex, int OwnerId,
    int? WeightGrams, decimal? PricePaid,
    PaymentStatus PaymentStatus, SpoolStatus SpoolStatus, string? Notes);

record SpoolUpdateRequest(
    string Name, string Material, string? ColorHex, int OwnerId,
    int? WeightGrams, decimal? PricePaid,
    PaymentStatus PaymentStatus, SpoolStatus SpoolStatus, string? Notes);
