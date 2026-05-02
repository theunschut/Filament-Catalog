using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Threading.Channels;

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

    builder.Services.AddScoped<IOwnerService, OwnerService>();
    builder.Services.AddScoped<ISpoolService, SpoolService>();
    builder.Services.AddScoped<ISummaryService, SummaryService>();

    // ---- Sync pipeline DI (per CLAUDE.md BackgroundService + Channel pattern) ----
    var syncChannel = Channel.CreateBounded<SyncJob>(
        new BoundedChannelOptions(capacity: 1)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });
    builder.Services.AddSingleton(syncChannel);
    builder.Services.AddSingleton<SyncStateService>();
    builder.Services.AddHttpClient<SyncService>();   // Named HttpClient scoped to SyncService
    builder.Services.AddScoped<ISyncService, SyncService>();
    builder.Services.AddHostedService<SyncBackgroundService>();

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

    app.MapControllers();

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
