# Stack Research

**Project:** Filament Catalog
**Researched:** 2026-04-30

---

## Recommended Versions

All packages verified against NuGet.org as of 2026-04-30.

| Package | Version | Purpose | Confidence |
|---------|---------|---------|------------|
| `Microsoft.EntityFrameworkCore` | 10.0.7 | ORM core | HIGH — confirmed NuGet |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.7 | SQLite provider | HIGH — confirmed NuGet |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.7 | `dotnet ef` CLI tool support | HIGH — confirmed NuGet |
| `Microsoft.Extensions.Hosting.WindowsServices` | 10.0.7 | Windows service lifetime | HIGH — confirmed NuGet |
| `AngleSharp` | 1.4.0 | HTML scraping | HIGH — confirmed NuGet, released 2025-11-12 |
| `SixLabors.ImageSharp` | 3.1.12 | Image processing / color extraction | HIGH — confirmed NuGet, released 2025-10-29 |

**Tool package (dev only):**

| Package | Version | Purpose |
|---------|---------|---------|
| `dotnet-ef` (global tool) | 10.0.7 | Migration CLI |

Install with: `dotnet tool install --global dotnet-ef --version 10.0.x`

**AngleSharp .NET 10 note:** AngleSharp 1.4.0 lists `net8.0` and `netstandard2.0` as explicit targets but is fully compatible with .NET 10 via `netstandard2.0` fallback. A beta 1.4.1 existed that fixed the NuGet `.targets` for `.NET 10` explicit TFM, but 1.4.0 works fine in practice since `netstandard2.0` resolves cleanly. Stick with 1.4.0 stable.

**ImageSharp licensing note:** SixLabors.ImageSharp uses a split license — Apache 2.0 for qualifying open source projects, but a commercial license is required for commercial use. For a personal local tool (filament tracker for personal use) this is not an issue. Be aware if this ever becomes a distributed product.

---

## Key Patterns

### 1. Windows Service — Program.cs skeleton

Use `Microsoft.NET.Sdk.Web` (not Worker SDK) since this is a web app with HTTP endpoints.
Call `builder.Services.AddWindowsService()` — not the older `UseWindowsService()` extension on `IHostBuilder`.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();  // sets WindowsServiceLifetime, sets ContentRoot to AppContext.BaseDirectory

// ... register services

var app = builder.Build();

// ... map endpoints, middleware

app.Run();
```

**Project file — disable web.config generation (not needed for Windows service):**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTransformWebConfigDisabled>true</IsTransformWebConfigDisabled>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>   <!-- for self-contained deployment -->
  </PropertyGroup>
</Project>
```

**Key behavior of `AddWindowsService()`:**
- When running as a service: sets `ContentRoot` to `AppContext.BaseDirectory` (the folder containing the .exe). This fixes the `C:\Windows\System32` current-directory problem.
- When running interactively (debug): behaves as a normal console app. Safe to always call.
- Enables Windows Event Log logging at Warning+ level by default.

### 2. URL Binding for localhost:5000

Default binding is `http://localhost:5000`. For a personal local tool this is fine and requires no extra config. To hard-code it explicitly:

```csharp
// Option A: in appsettings.json (preferred — survives redeployment without recompile)
// "Kestrel": { "Endpoints": { "Http": { "Url": "http://localhost:5000" } } }

// Option B: programmatic
builder.WebHost.UseUrls("http://localhost:5000");
```

Do not use `http://*:5000` for a purely local service — `localhost` is sufficient and avoids Windows Firewall prompts.

### 3. Registering the Windows Service

From an admin PowerShell prompt after publishing:

```powershell
New-Service -Name "FilamentCatalog" `
            -BinaryPathName "C:\Services\FilamentCatalog\FilamentCatalog.exe" `
            -DisplayName "Filament Catalog" `
            -Description "Bambu Lab filament spool inventory tracker" `
            -StartupType Automatic

Start-Service -Name "FilamentCatalog"
```

### 4. EF Core + SQLite setup

**DbContext configuration:**

```csharp
// In Program.cs / service registration
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var dbPath = Path.Combine(
        builder.Environment.ContentRootPath,  // resolves to AppContext.BaseDirectory when running as service
        "filament.db"
    );
    options.UseSqlite($"Data Source={dbPath}");
});
```

**Do not use `Directory.GetCurrentDirectory()` or relative paths** — when running as a Windows service the working directory is `C:\Windows\System32`. Always construct an absolute path from `ContentRootPath` or `AppContext.BaseDirectory`.

**Applying migrations at startup (recommended for this single-instance local app):**

```csharp
// After app = builder.Build(), before app.Run():
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

This is safe for a single-instance local app. EF Core 9+ added migration locking so concurrent migrations don't corrupt the DB, but that is not a concern here.

Do NOT call `EnsureCreatedAsync()` — it bypasses migrations and will conflict if migrations exist.

**Development workflow:**
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update   # for local dev runs
# At service startup, MigrateAsync() keeps the deployed DB up to date automatically
```

### 5. Static file serving (plain HTML/CSS/JS frontend)

Place files in `wwwroot/`. The minimal API default pipeline handles it:

```csharp
app.UseDefaultFiles();   // must come BEFORE UseStaticFiles — serves index.html for "/"
app.UseStaticFiles();
```

`UseDefaultFiles()` makes `GET /` serve `wwwroot/index.html` without a redirect. Without it, you'd need to navigate to `/index.html` explicitly.

When running as a Windows service, `wwwroot/` is resolved relative to `ContentRootPath` (which `AddWindowsService()` sets to `AppContext.BaseDirectory`). The `wwwroot` folder must be deployed alongside the executable.

### 6. ImageSharp dominant color extraction — approach

ImageSharp does not have a one-call "dominant color" API. The correct approach for a swatch image (small, limited palette):

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

static string GetDominantHex(byte[] imageBytes)
{
    using var image = Image.Load<Rgb24>(imageBytes);

    // Resize to tiny (16x16) to reduce noise before sampling
    image.Mutate(x => x.Resize(16, 16));

    var colorCounts = new Dictionary<(byte R, byte G, byte B), int>();
    for (int y = 0; y < image.Height; y++)
    {
        for (int x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            // Quantize slightly to merge near-identical shades
            var key = ((byte)(pixel.R & 0xF8), (byte)(pixel.G & 0xF8), (byte)(pixel.B & 0xF8));
            colorCounts[key] = colorCounts.GetValueOrDefault(key) + 1;
        }
    }

    var dominant = colorCounts.MaxBy(kvp => kvp.Value).Key;
    return $"#{dominant.R:X2}{dominant.G:X2}{dominant.B:X2}";
}
```

The resize-then-histogram pattern is the standard approach documented in community examples. For Bambu swatch images (solid color patches) this is more than sufficient.

---

## Gotchas and Warnings

### Critical

**1. Windows service current directory is `C:\Windows\System32`**
Any relative file path (SQLite `Data Source=filament.db`, `File.ReadAllText("settings.json")`) will resolve to system32 and either fail or create the file in the wrong place. Always use `ContentRootPath` or `AppContext.BaseDirectory` for any file I/O.

**2. `EnsureCreatedAsync()` breaks migrations**
Calling `EnsureCreatedAsync()` before `MigrateAsync()` creates the schema without the migrations history table. Subsequent `MigrateAsync()` calls will fail. Only ever use `MigrateAsync()`.

**3. SQLite abandoned migration lock**
If the process is killed mid-migration, EF Core 9+ leaves a `__EFMigrationsLock` table row. Subsequent startups will hang indefinitely waiting for the lock. Recovery: open the SQLite file and run `DROP TABLE "__EFMigrationsLock";`. For a local personal app this is a recoverable one-time edge case, not a blocker.

**4. `UseDefaultFiles()` order matters**
Must be called before `UseStaticFiles()`. Reversing the order means `GET /` returns a 404 and you must navigate to `/index.html` manually.

### Moderate

**5. AngleSharp targets `net8.0` explicitly, not `net10.0`**
Works fine via `netstandard2.0` fallback but the NuGet package metadata lists `net8.0` as the highest explicit TFM. No runtime issues expected — this is a packaging labeling detail, not a compatibility problem.

**6. SQLite column type limitations**
SQLite does not support server-side `decimal` comparisons or ordering — EF evaluates these in-process. For this app (prices stored as `decimal`) avoid sorting large result sets by price in LINQ unless you are okay with client-side evaluation. Use `double` if price sort performance matters (it won't at this scale).

**7. SQLite migration schema rebuild**
Operations like `DropColumn`, `AlterColumn`, `AddForeignKey` trigger a full table rebuild (SQLite limitation). EF Core handles this automatically but the migration is slower and generates more SQL than on SQL Server. Not a problem for a local app with tiny data volumes, but worth knowing when reading generated migrations.

**8. `IsTransformWebConfigDisabled` required for Web SDK + Windows service**
Without `<IsTransformWebConfigDisabled>true</IsTransformWebConfigDisabled>` in the project file, the publish pipeline generates a `web.config` that is meaningless (and potentially confusing) for a Windows service deployment.

### Minor

**9. Event Log source creation requires admin on first run**
`AddWindowsService()` enables Event Log logging. If the application event source doesn't exist, .NET will attempt to create it — which requires admin rights. Running the service under a non-admin account may log a warning and skip event log entries on first run. Not a blocker for a personal tool running as LocalSystem.

**10. ImageSharp commercial license**
Split license model. Fine for personal use. If app is ever distributed, a commercial license is needed.

**11. .NET 10 minimal API validation is opt-in**
`builder.Services.AddValidation()` enables DataAnnotations-based request validation. It is not enabled by default — existing `[Required]` attributes on request models do nothing without calling `AddValidation()`. This is a .NET 10 addition absent in .NET 8/9.

---

## Confidence Notes

| Area | Confidence | Source |
|------|------------|--------|
| Package versions (EF Core, WindowsServices) | HIGH | NuGet.org verified 2026-04-30 |
| Package versions (AngleSharp, ImageSharp) | HIGH | NuGet.org verified |
| `AddWindowsService()` vs `UseWindowsService()` | HIGH | Official Microsoft Learn docs (updated 2026-04-29) |
| Content root / BaseDirectory behavior | HIGH | Official Microsoft Learn docs |
| URL binding (`localhost:5000` default) | HIGH | Official docs |
| `MigrateAsync()` at startup pattern | HIGH | Official EF Core docs |
| Migration locking / abandoned lock on SQLite | HIGH | Official EF Core SQLite limitations docs |
| SQLite type limitations (`decimal`, `DateTimeOffset`) | HIGH | Official EF Core SQLite limitations docs |
| `.NET 10` minimal API differences from .NET 8/9 | HIGH | Official `What's new in ASP.NET Core 10` docs |
| `UseDefaultFiles` + `UseStaticFiles` ordering | HIGH | Official ASP.NET Core static files docs |
| ImageSharp dominant color approach | MEDIUM | Community examples + docs; no official "dominant color" API exists |
| AngleSharp .NET 10 compatibility via fallback | MEDIUM | NuGet TFM list inspection + community reports |

---

## Sources

- [Host ASP.NET Core in a Windows Service — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0) (updated 2026-04-29)
- [What's new in ASP.NET Core 10 — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0)
- [EF Core SQLite Provider — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [EF Core SQLite Limitations — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations) (updated 2026-04-16)
- [Applying EF Core Migrations — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) (updated 2026-04-16)
- [Static files in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- [NuGet: Microsoft.EntityFrameworkCore 10.0.7](https://www.nuget.org/packages/microsoft.entityframeworkcore)
- [NuGet: Microsoft.EntityFrameworkCore.Sqlite 10.0.7](https://www.nuget.org/packages/microsoft.entityframeworkcore.sqlite)
- [NuGet: Microsoft.Extensions.Hosting.WindowsServices 10.0.7](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.WindowsServices)
- [NuGet: AngleSharp 1.4.0](https://www.nuget.org/packages/AngleSharp)
- [NuGet: SixLabors.ImageSharp 3.1.12](https://www.nuget.org/packages/sixlabors.imagesharp/)
