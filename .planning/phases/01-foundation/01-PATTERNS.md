# Phase 1: Foundation - Pattern Map

**Mapped:** 2026-04-30
**Files analyzed:** 9 (new files — greenfield project)
**Analogs found:** 0 / 9 (no existing source code)

> This is a brand-new project. There are no existing source files to use as analogs.
> All patterns are sourced from CLAUDE.md conventions and RESEARCH.md documented patterns.
> Code excerpts below are the canonical patterns the planner MUST instruct implementors to copy verbatim.

---

## File Classification

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `FilamentCatalog/FilamentCatalog.csproj` | config | — | none | no analog |
| `FilamentCatalog/Program.cs` | entrypoint | request-response | none | no analog |
| `FilamentCatalog/AppDbContext.cs` | model | CRUD | none | no analog |
| `FilamentCatalog/Models/Owner.cs` | model | CRUD | none | no analog |
| `FilamentCatalog/appsettings.json` | config | — | none | no analog |
| `FilamentCatalog/wwwroot/index.html` | component (static) | request-response | none | no analog |
| `FilamentCatalog/Migrations/` | migration | CRUD | none | no analog (EF-generated) |
| `install.ps1` | utility (script) | — | none | no analog |
| `uninstall.ps1` | utility (script) | — | none | no analog |

---

## Pattern Assignments

### `FilamentCatalog/FilamentCatalog.csproj` (config)

**Source:** RESEARCH.md — "Complete csproj" example

**Full file pattern:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsTransformWebConfigDisabled>true</IsTransformWebConfigDisabled>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="10.0.7" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.7" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.7">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
  </ItemGroup>
</Project>
```

**Key rules:**
- SDK MUST be `Microsoft.NET.Sdk.Web` — not `Microsoft.NET.Sdk.Worker`
- `<IsTransformWebConfigDisabled>true</IsTransformWebConfigDisabled>` suppresses spurious `web.config` generation
- `Microsoft.EntityFrameworkCore.Design` gets `<PrivateAssets>all</PrivateAssets>` — design-time only, not deployed
- `Serilog.Sinks.File` is a transitive dependency of `Serilog.AspNetCore`; no explicit reference needed

---

### `FilamentCatalog/Program.cs` (entrypoint, request-response)

**Source:** RESEARCH.md — "Pattern 1: ASP.NET Core + Windows Service" + CLAUDE.md critical conventions

**Full file pattern:**
```csharp
using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;

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

    builder.Host.UseSerilog();                         // replaces all ILogger sinks

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "FilamentCatalog";
    });

    var dbPath = Path.Combine(AppContext.BaseDirectory, "filament.db");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    var app = builder.Build();

    // Run migrations + seed BEFORE app.Run()
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ClearStaleEfMigrationsLock(db);
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    app.UseDefaultFiles();   // MUST be before UseStaticFiles
    app.UseStaticFiles();

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
        // Table doesn't exist on first run; that is fine
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
```

**Critical ordering rules (CLAUDE.md):**
- `app.UseDefaultFiles()` MUST come before `app.UseStaticFiles()` — non-negotiable
- `ClearStaleEfMigrationsLock` MUST be called before `MigrateAsync()`
- All startup DB work MUST complete before `app.RunAsync()`
- SQLite path MUST use `AppContext.BaseDirectory`, never a relative path
- Log path MUST use `AppContext.BaseDirectory`, never a relative path
- Timestamps MUST use `DateTime.UtcNow`, never `DateTime.Now`

---

### `FilamentCatalog/AppDbContext.cs` (model, CRUD)

**Source:** RESEARCH.md — "Pattern 4: Owner Entity (Phase 1 schema)"

**Full file pattern:**
```csharp
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Owner> Owners => Set<Owner>();
}
```

**Rules:**
- Phase 1 contains only the `Owners` DbSet
- Each subsequent phase adds its own DbSet via its own migration — do not add Phase 2+ sets here

---

### `FilamentCatalog/Models/Owner.cs` (model, CRUD)

**Source:** RESEARCH.md — "Pattern 4: Owner Entity (Phase 1 schema)"

**Full file pattern:**
```csharp
public class Owner
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsMe { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Rules:**
- `CreatedAt` stores `DateTime.UtcNow` — never local time (CLAUDE.md hard rule)
- `Name` uses `required` keyword (C# 11+ / .NET 8+ pattern with nullable enabled)
- No navigation properties in Phase 1; future phases add them via new migrations

---

### `FilamentCatalog/appsettings.json` (config)

**Source:** RESEARCH.md — "Pattern 6: appsettings.json for URL binding"

**Full file pattern:**
```json
{
  "Urls": "http://localhost:5000",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Rules:**
- `"Urls"` key binds Kestrel to `http://localhost:5000` — this is the canonical local-only binding
- Serilog intercepts `ILogger` calls at runtime; these `LogLevel` entries serve as fallback only

---

### `FilamentCatalog/wwwroot/index.html` (static component, request-response)

**Source:** Claude's Discretion (D-01) — minimal structural HTML skeleton

**Pattern:** Plain HTML5, no framework, no build step. Should be a structural skeleton that Phase 2 extends rather than a throwaway "hello world". Include a `<main>` with a loading placeholder and a comment marking where Phase 2 content goes.

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Filament Catalog</title>
  <style>
    /* Phase 1 placeholder styles — extend in Phase 2 */
    body { font-family: system-ui, sans-serif; margin: 0; padding: 2rem; background: #f5f5f5; }
    h1 { color: #333; }
    .status { color: #666; font-size: 0.9rem; }
  </style>
</head>
<body>
  <h1>Filament Catalog</h1>
  <!-- Phase 2: replace this placeholder with actual UI -->
  <main>
    <p class="status">Service is running.</p>
  </main>
  <!-- Phase 2: add ES module scripts here -->
</body>
</html>
```

**Rules:**
- No framework, no build step — plain HTML/CSS/JS (CLAUDE.md)
- ES modules for any JS added in Phase 2+ (CLAUDE.md: "ES modules")
- Native `<dialog>` for modals in future phases (CLAUDE.md)
- File lives at `wwwroot/index.html` — served by `UseDefaultFiles` + `UseStaticFiles`

---

### `FilamentCatalog/Migrations/` (migration — EF Core generated)

**Source:** RESEARCH.md — "Standard Stack", "Pattern 2: EF Core Migration + Seed Startup"

**Generation command (run after project builds):**
```bash
dotnet ef migrations add InitialCreate --project FilamentCatalog
```

**Rules:**
- NEVER hand-write migration files — always use `dotnet ef migrations add`
- Migration will create `__EFMigrationsHistory` and `__EFMigrationsLock` tables automatically
- The `ClearStaleEfMigrationsLock` guard in `Program.cs` handles the lock table
- NEVER use `EnsureCreatedAsync()` alongside migrations — it bypasses migration history

---

### `install.ps1` (utility script, repo root)

**Source:** RESEARCH.md — "Pattern 5: install.ps1"

**Full file pattern:**
```powershell
#Requires -RunAsAdministrator
param(
    [string]$PublishDir = "$PSScriptRoot\publish"
)

$exePath = Join-Path $PublishDir "FilamentCatalog.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at '$exePath'. Run 'dotnet publish' first."
    exit 1
}

New-Service -Name "FilamentCatalog" `
            -BinaryPathName $exePath `
            -DisplayName "Filament Catalog" `
            -Description "Local filament spool tracker" `
            -StartupType Automatic

Start-Service -Name "FilamentCatalog"
Write-Host "Service installed and started. Browse to http://localhost:5000"
```

**Rules:**
- `#Requires -RunAsAdministrator` prevents silent failure when not elevated
- `$PublishDir` defaults to `$PSScriptRoot\publish` — always points to published output, never debug build
- Guard against missing exe to give a clear error (avoids Pitfall 4 from RESEARCH.md)
- Uses `New-Service` (PowerShell-idiomatic); falls back to `sc.exe create` if needed

---

### `uninstall.ps1` (utility script, repo root)

**Source:** RESEARCH.md — "Pattern 5: uninstall.ps1"

**Full file pattern:**
```powershell
#Requires -RunAsAdministrator

Stop-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue
Remove-Service -Name "FilamentCatalog"
Write-Host "Service removed."
```

**Rules:**
- `-ErrorAction SilentlyContinue` on `Stop-Service` handles already-stopped service gracefully
- `Remove-Service` requires PowerShell 6+ or Windows PowerShell 5.1 with KB update; if unavailable, use `sc.exe delete FilamentCatalog`

---

## Shared Patterns

### AppContext.BaseDirectory — All File Paths
**Source:** CLAUDE.md critical convention + RESEARCH.md Pitfall 1
**Apply to:** `Program.cs` (DB path, log path), any future file I/O

```csharp
// CORRECT — works when running as Windows service
Path.Combine(AppContext.BaseDirectory, "filament.db")
Path.Combine(AppContext.BaseDirectory, "logs", "catalog-.log")

// WRONG — resolves to C:\Windows\System32 when running as service
"filament.db"
"./logs/"
Path.Combine(Directory.GetCurrentDirectory(), "filament.db")
```

### DateTime.UtcNow — All Timestamps
**Source:** CLAUDE.md hard rule + RESEARCH.md Pitfall 5
**Apply to:** `Models/Owner.cs`, all future entity models

```csharp
// CORRECT
CreatedAt = DateTime.UtcNow

// WRONG — breaks EF Core 10 SQLite timezone handling
CreatedAt = DateTime.Now
```

### Stale Migration Lock Guard
**Source:** CLAUDE.md critical convention + RESEARCH.md Pattern 3 and Pitfall 2
**Apply to:** `Program.cs` startup sequence (before every `MigrateAsync()` call)

```csharp
static async Task ClearStaleEfMigrationsLock(AppDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsLock\"");
    }
    catch
    {
        // Table doesn't exist on first run; that is fine
    }
}
```

### Middleware Ordering
**Source:** CLAUDE.md critical convention + RESEARCH.md Pitfall 3
**Apply to:** `Program.cs` middleware pipeline

```csharp
// CORRECT ORDER — UseDefaultFiles MUST precede UseStaticFiles
app.UseDefaultFiles();
app.UseStaticFiles();

// WRONG — UseStaticFiles first causes / to 404
app.UseStaticFiles();
app.UseDefaultFiles();  // too late; already tried and failed
```

---

## No Analog Found

All files in this phase have no existing codebase analog — this is a greenfield project.

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `FilamentCatalog/FilamentCatalog.csproj` | config | — | No source files exist yet |
| `FilamentCatalog/Program.cs` | entrypoint | request-response | No source files exist yet |
| `FilamentCatalog/AppDbContext.cs` | model | CRUD | No source files exist yet |
| `FilamentCatalog/Models/Owner.cs` | model | CRUD | No source files exist yet |
| `FilamentCatalog/appsettings.json` | config | — | No source files exist yet |
| `FilamentCatalog/wwwroot/index.html` | component | request-response | No source files exist yet |
| `FilamentCatalog/Migrations/` | migration | CRUD | EF Core generated; no source files exist yet |
| `install.ps1` | utility | — | No source files exist yet |
| `uninstall.ps1` | utility | — | No source files exist yet |

**Resolution:** Use the verbatim patterns documented in each file's "Pattern Assignments" section above, all of which are sourced from RESEARCH.md (verified against official docs) and CLAUDE.md (project hard rules).

---

## Metadata

**Analog search scope:** Entire repository (`D:\repos\Fillament Catalog\`)
**Files scanned:** 0 source files (project is greenfield — only `.planning/` docs and CLAUDE.md exist)
**Pattern extraction date:** 2026-04-30
**Pattern source:** CLAUDE.md + 01-RESEARCH.md (verified against official Microsoft and Serilog docs)
