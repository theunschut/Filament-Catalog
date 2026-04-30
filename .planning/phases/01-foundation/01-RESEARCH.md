# Phase 1: Foundation - Research

**Researched:** 2026-04-30
**Domain:** .NET 10 Windows Service + ASP.NET Core Minimal API + EF Core 10 + SQLite + Serilog
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-02: Create only the `Owner` table in Phase 1. Each subsequent phase adds its own migration.
- D-03: Serilog file logging, writing to a `logs/` directory next to the executable (`AppContext.BaseDirectory`).
- D-04: Log files separated by date (rolling daily).
- D-05: Auto-delete log files older than 7 days.
- D-06: Include `install.ps1` and `uninstall.ps1` scripts at repo root.

### Claude's Discretion
- Exact placeholder page content/layout
- Serilog sink choice (File vs RollingFile — use whichever supports daily rolling + retention natively)
- PowerShell script style (sc.exe vs New-Service cmdlet)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INFRA-01 | App runs as a Windows service (auto-starts with Windows, accessible at `http://localhost:5000`) | Windows Service hosting via `AddWindowsService()` + `New-Service` install script |
| INFRA-02 | SQLite DB stored relative to executable using `AppContext.BaseDirectory` | Verified connection string pattern; `AddWindowsService` sets ContentRoot to `AppContext.BaseDirectory` |
| INFRA-03 | `index.html` served as static files via `UseDefaultFiles` + `UseStaticFiles` | Verified middleware ordering; `MapStaticAssets` (ASP.NET Core 10 default) does NOT support default documents — must use `UseDefaultFiles` + `UseStaticFiles` |
| OWNER-03 | On first run, seed one Owner with `IsMe = true`, Name = "Me" | Custom seed method (check `Any()`, insert if empty) called after `MigrateAsync()` |
</phase_requirements>

---

## Summary

Phase 1 creates the entire structural foundation from scratch: a .NET 10 project using `Microsoft.NET.Sdk.Web` that runs as a Windows service, serves a static placeholder page, and maintains a SQLite database with a single `Owner` table seeded with a "Me" record.

The key architectural insight is that this project uses **`Microsoft.NET.Sdk.Web`** (not `Microsoft.NET.Sdk.Worker`) because it needs ASP.NET Core middleware and static file serving. The `AddWindowsService()` call in `Program.cs` is the only addition needed to make a standard web app also run as a Windows service — it sets the host lifetime, content root, and event log logging when the process is running under the SCM.

For static files, ASP.NET Core 10 introduced `MapStaticAssets()` as the recommended approach, but it does **not** support default document serving (the feature that rewrites `/` to `/index.html`). The project must use the older `UseDefaultFiles()` + `UseStaticFiles()` pair. This is confirmed in official docs: default document serving is listed as a feature exclusive to Static File Middleware, not MapStaticAssets.

**Primary recommendation:** Use `Microsoft.NET.Sdk.Web`, `WebApplication.CreateBuilder`, `builder.Services.AddWindowsService()`, `app.UseDefaultFiles()` before `app.UseStaticFiles()`, and `MigrateAsync()` + custom seed on startup.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Windows service lifecycle | Host (Generic Host) | — | `AddWindowsService()` integrates with SCM; no separate tier |
| HTTP listening on :5000 | ASP.NET Core (Kestrel) | — | Default binding; configurable via `appsettings.json` or env var |
| Static file serving (index.html) | ASP.NET Core Middleware | — | `UseDefaultFiles` + `UseStaticFiles` in pipeline |
| Database schema + seeding | EF Core (startup) | SQLite | `MigrateAsync()` + seed method before `host.Run()` |
| Structured logging | Serilog (file sink) | — | Serilog intercepts all `ILogger` calls, writes to rolling file |
| Service install/uninstall | PowerShell scripts | sc.exe / New-Service | External scripts; not part of the running app |

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.7 | Windows SCM integration via `AddWindowsService()` | Official Microsoft package for .NET Windows services |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.7 | ORM + SQLite provider | Official EF Core provider for SQLite |
| Microsoft.EntityFrameworkCore.Design | 10.0.7 | `dotnet ef` CLI tooling (migrations add, etc.) | Required for EF Core design-time tools |
| Serilog.AspNetCore | 10.0.0 | Routes all `ILogger` calls through Serilog | Standard Serilog/ASP.NET Core integration package |
| Serilog.Sinks.File | 7.0.0 | Rolling file sink with daily interval + retention | Included automatically via Serilog.AspNetCore |

[VERIFIED: nuget.org registry — `dotnet package search` run 2026-04-30]

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `UseDefaultFiles` + `UseStaticFiles` | `MapStaticAssets` | `MapStaticAssets` is the new ASP.NET Core 10 default and has better compression/fingerprinting, but it does NOT support default document serving (`/` → `index.html`) — must use the middleware pair |
| `Microsoft.NET.Sdk.Web` | `Microsoft.NET.Sdk.Worker` | Worker SDK is for background-only services; Web SDK is required for ASP.NET Core middleware and Kestrel |
| `New-Service` PowerShell | `sc.exe create` | Both work; `New-Service` is more PowerShell-idiomatic with parameters; `sc.exe` is more universally available and scriptable |

**Installation:**
```bash
dotnet new web -n FilamentCatalog
dotnet add package Microsoft.Extensions.Hosting.WindowsServices --version 10.0.7
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.7
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.7
dotnet add package Serilog.AspNetCore --version 10.0.0
```

---

## Architecture Patterns

### System Architecture Diagram

```
[Windows SCM]
     |
     | sc start / stop
     v
[FilamentCatalog.exe] (Kestrel on :5000)
     |
     +--[Startup sequence]
     |    1. Log.Logger configured (Serilog -> logs/catalog-YYYYMMDD.log)
     |    2. builder.Services.AddWindowsService()
     |    3. builder.Services.AddDbContext<AppDbContext>()
     |    4. host.Build()
     |    5. MigrateAsync() -- applies pending migrations
     |    6. SeedAsync()    -- inserts "Me" owner if empty
     |    7. host.Run()
     |
     +--[Request pipeline]
          |
          +--> UseDefaultFiles()   -- rewrites / to /index.html
          +--> UseStaticFiles()    -- serves wwwroot files
          +--> (future API endpoints go here in Phase 2+)
```

### Recommended Project Structure
```
FilamentCatalog/
├── FilamentCatalog.csproj       # Sdk="Microsoft.NET.Sdk.Web"
├── Program.cs                   # entry point, DI, middleware, seed
├── AppDbContext.cs              # EF Core DbContext
├── Models/
│   └── Owner.cs                 # Owner entity (Phase 1 only)
├── Migrations/                  # EF Core generated migrations
│   └── YYYYMMDD_InitialCreate.cs
├── wwwroot/
│   └── index.html               # placeholder page
├── appsettings.json             # Kestrel URL, logging min levels
└── (repo root)
    ├── install.ps1
    └── uninstall.ps1
```

### Pattern 1: ASP.NET Core + Windows Service (Program.cs)
**What:** Combining a standard `WebApplication` host with Windows service integration.
**When to use:** Any ASP.NET Core app that needs to run as a Windows service.
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "catalog-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();                         // replaces all ILogger sinks
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "FilamentCatalog";
    });
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(
            $"Data Source={Path.Combine(AppContext.BaseDirectory, "filament.db")}"));

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
```

### Pattern 2: EF Core Migration + Seed Startup
**What:** Apply pending migrations and seed default data before accepting requests.
**When to use:** Every startup — `MigrateAsync()` is idempotent.
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
static async Task SeedAsync(AppDbContext db)
{
    if (!await db.Owners.AnyAsync())
    {
        db.Owners.Add(new Owner { Name = "Me", IsMe = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}
```

### Pattern 3: Stale EF Migrations Lock Guard
**What:** Clear abandoned `__EFMigrationsLock` rows before calling `MigrateAsync()`.
**When to use:** Mandatory on startup — protects against crash-during-migration leaving a dangling lock.
```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations#handling-abandoned-locks
static async Task ClearStaleEfMigrationsLock(AppDbContext db)
{
    try
    {
        // Table may not exist on first run — ignore errors
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"__EFMigrationsLock\"");
    }
    catch
    {
        // Table doesn't exist yet; that is fine
    }
}
```

### Pattern 4: Owner Entity (Phase 1 schema)
```csharp
// Phase 1 schema — only Owner table
public class Owner
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsMe { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Owner> Owners => Set<Owner>();
}
```

### Pattern 5: install.ps1 / uninstall.ps1
```powershell
# install.ps1 — run as Administrator after dotnet publish
param(
    [string]$PublishDir = "$PSScriptRoot\publish"
)
$exePath = Join-Path $PublishDir "FilamentCatalog.exe"
New-Service -Name "FilamentCatalog" `
            -BinaryPathName $exePath `
            -DisplayName "Filament Catalog" `
            -Description "Local filament spool tracker" `
            -StartupType Automatic
Start-Service -Name "FilamentCatalog"
Write-Host "Service installed and started. Browse to http://localhost:5000"
```

```powershell
# uninstall.ps1 — run as Administrator
Stop-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue
Remove-Service -Name "FilamentCatalog"
Write-Host "Service removed."
```

### Pattern 6: appsettings.json for URL binding
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

### Anti-Patterns to Avoid
- **Using `GetCurrentDirectory()` for file paths:** Returns `C:\Windows\System32` when running as a service. Always use `AppContext.BaseDirectory`.
- **Calling `EnsureCreatedAsync()` instead of `MigrateAsync()`:** `EnsureCreatedAsync()` bypasses the migrations history table and will cause `MigrateAsync()` to fail on subsequent runs.
- **Putting `UseStaticFiles()` before `UseDefaultFiles()`:** The rewrite must happen first or `/` will 404 instead of serving `index.html`.
- **Using `Microsoft.NET.Sdk.Worker` SDK:** Won't include ASP.NET Core middleware pipeline or Kestrel HTTP server. Use `Microsoft.NET.Sdk.Web`.
- **Using `MapStaticAssets()` for default documents:** The ASP.NET Core 10 `MapStaticAssets()` call does not handle default document serving. Stick with the `UseDefaultFiles` + `UseStaticFiles` middleware pair.
- **Wrapping `MigrateAsync()` in an explicit transaction:** Not supported in EF Core 9+ (documented breaking change).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Rolling log files with retention | Custom `FileStream` + file rotation logic | `Serilog.Sinks.File` with `rollingInterval` + `retainedFileCountLimit` | Edge cases: UTC midnight rollover, file locking, concurrent access |
| Migration locking | Custom lock table / mutex | EF Core 9+ built-in migration locking | Automatic; tested against race conditions |
| Windows service lifecycle (start/stop/pause signals) | `ServiceBase` override | `AddWindowsService()` | Handles all SCM signals correctly; integrates with `IHostApplicationLifetime` |
| Database seeding | `HasData()` in `OnModelCreating` | Custom `SeedAsync()` method called after `MigrateAsync()` | `HasData()` uses migration-based seeding (baked into migration SQL), which complicates re-seeding logic; a runtime check (`AnyAsync()`) is simpler and idempotent |

**Key insight:** The Windows service + migrations + Serilog stack has well-worn patterns in official docs. Every custom solution in this domain introduces edge cases that the official tools already handle.

---

## Common Pitfalls

### Pitfall 1: Working Directory is C:\Windows\System32
**What goes wrong:** Any relative file path (`"filament.db"`, `"logs/"`) resolves to `C:\Windows\System32\` when the process is running as a Windows service, causing access-denied errors or files created in the wrong location.
**Why it happens:** Windows services start with the system directory as their working directory.
**How to avoid:** Use `AppContext.BaseDirectory` for all file paths. `AddWindowsService()` also sets `ContentRootPath` to `AppContext.BaseDirectory` automatically.
**Warning signs:** DB file not created next to exe; logs appear in `C:\Windows\System32`.

### Pitfall 2: Stale __EFMigrationsLock on Startup
**What goes wrong:** App won't start; `MigrateAsync()` hangs indefinitely waiting for a lock that will never be released.
**Why it happens:** EF Core 9+ uses a `__EFMigrationsLock` table for SQLite. If the process is killed mid-migration (crash, forced stop), the lock row is left behind.
**How to avoid:** Always execute `DELETE FROM "__EFMigrationsLock"` (catching table-not-found) before `MigrateAsync()`.
**Warning signs:** App starts but hangs silently after launch; no log output from the migration step.

### Pitfall 3: UseStaticFiles Before UseDefaultFiles
**What goes wrong:** Navigating to `http://localhost:5000/` returns a 404 instead of `index.html`.
**Why it happens:** `UseDefaultFiles()` is a URL rewriter — it must rewrite the URL before `UseStaticFiles()` tries to serve it. If the order is reversed, `UseStaticFiles()` looks for a file named `/` (which doesn't exist) and returns 404, and `UseDefaultFiles()` never runs.
**How to avoid:** Always call `app.UseDefaultFiles()` immediately before `app.UseStaticFiles()`.
**Warning signs:** Direct navigation to `http://localhost:5000/index.html` works but `/` returns 404.

### Pitfall 4: Registering the Service Pointing to Debug Build
**What goes wrong:** Service fails to start or behaves unexpectedly.
**Why it happens:** The install script points to the debug output (`bin/Debug/`). Debug builds have different file layouts and may reference files that don't exist in production layout.
**How to avoid:** Always `dotnet publish` before running `install.ps1`. The install script should reference the publish output directory, not the build output.
**Warning signs:** Service installs but immediately enters "Stopped" state; Event Log shows file-not-found errors.

### Pitfall 5: DateTime.Now Instead of DateTime.UtcNow
**What goes wrong:** EF Core 10 with SQLite throws timezone-related exceptions when reading back `DateTime` values stored with local timezone info.
**Why it happens:** EF Core 10 SQLite breaking change — timezone conversion behavior was made stricter.
**How to avoid:** Always use `DateTime.UtcNow` for all entity timestamps. This is a hard project rule per `CLAUDE.md`.
**Warning signs:** `InvalidOperationException` or silent data corruption when querying DateTime columns.

---

## Code Examples

### Complete csproj
```xml
<!-- Source: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service -->
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

### Serilog Setup (rolling daily, 7-day retention)
```csharp
// Source: https://github.com/serilog/serilog-aspnetcore README + Serilog.Sinks.File docs
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

// In builder setup:
builder.Host.UseSerilog();
```

### UseDefaultFiles + UseStaticFiles (correct order)
```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files
// UseDefaultFiles MUST precede UseStaticFiles — it is a URL rewriter, not a file server
app.UseDefaultFiles();   // rewrites / -> /index.html (and /index.htm, etc.)
app.UseStaticFiles();    // serves the rewritten path from wwwroot/
```

### EF Core connection string with absolute path
```csharp
// Source: CLAUDE.md convention + MS docs on Windows service working directory
var dbPath = Path.Combine(AppContext.BaseDirectory, "filament.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `UseStaticFiles()` only (no default docs) | `UseDefaultFiles()` + `UseStaticFiles()` for SPA-style index.html | Always the pattern | No change — still correct |
| `MapStaticAssets()` (ASP.NET Core 10 default) | Must use `UseDefaultFiles` + `UseStaticFiles` when serving default docs | ASP.NET Core 9/10 | `MapStaticAssets` is better for fingerprinted assets but lacks default document support |
| Manual migration lock handling | EF Core 9+ built-in migration locking via `__EFMigrationsLock` | EF Core 9 (2024) | Adds automatic lock but abandoned locks must be cleared on startup |
| `UseWindowsService()` on `IHostBuilder` | `builder.Services.AddWindowsService()` on `WebApplicationBuilder` | .NET 6 minimal API era | Both work; `AddWindowsService()` is the minimal-API-compatible form |

**Deprecated/outdated:**
- `IHostBuilder.UseWindowsService()`: Replaced by `builder.Services.AddWindowsService()` in the minimal API / `WebApplication` pattern. Both work, but use the new form.
- `EnsureCreatedAsync()`: Not a migration-aware method; never use alongside Migrations.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Serilog.Sinks.File` is automatically included as a transitive dependency of `Serilog.AspNetCore 10.0.0` | Standard Stack | May need explicit `dotnet add package Serilog.Sinks.File` — low risk, caught at compile time |
| A2 | The `New-Service` cmdlet is available in the PowerShell version on the target machine (requires PowerShell 5.1+ on Windows) | Code Examples | If only PowerShell 4, fall back to `sc.exe create` — minimal impact |

---

## Open Questions

1. **`net10.0` vs `net10.0-windows` target framework**
   - What we know: The Windows service doc example uses `net10.0-windows` for Worker SDK projects; ASP.NET Core web apps typically use `net10.0`
   - What's unclear: Whether `net10.0-windows` is required for `AddWindowsService()` with the Web SDK
   - Recommendation: Use `net10.0` (plain); `AddWindowsService()` compiles fine on `net10.0` — Windows-specific APIs are gated on `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` internally. [ASSUMED]

2. **`<IsTransformWebConfigDisabled>true</IsTransformWebConfigDisabled>` requirement**
   - What we know: Official docs recommend this for Web SDK Windows service projects to suppress `web.config` generation
   - What's unclear: Whether it causes any issues with `dotnet publish` on .NET 10
   - Recommendation: Include it — suppresses a spurious file that is never used.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + run | ✓ | 10.0.202 | — |
| PowerShell (New-Service) | install.ps1 / uninstall.ps1 | ✓ (Windows 11) | Built-in | sc.exe create |
| sc.exe | Service management verification | ✓ (Windows 11) | Built-in | — |
| dotnet ef CLI | Migration generation | ✗ (not yet installed) | — | `dotnet tool install -g dotnet-ef` |

**Missing dependencies with no fallback:**
- `dotnet-ef` global tool must be installed before running `dotnet ef migrations add`. Install command: `dotnet tool install --global dotnet-ef`

**Missing dependencies with fallback:**
- None beyond the above.

---

## Project Constraints (from CLAUDE.md)

All of the following directives are mandatory and override any research recommendation:

| Constraint | Source | Applies To |
|------------|--------|------------|
| SQLite path: `Path.Combine(AppContext.BaseDirectory, "filament.db")` — never relative | CLAUDE.md | AppDbContext registration |
| `app.UseDefaultFiles()` MUST come before `app.UseStaticFiles()` | CLAUDE.md | Middleware order |
| Add startup guard to clear stale `__EFMigrationsLock` rows | CLAUDE.md | Program.cs startup |
| Call `MigrateAsync()` on startup | CLAUDE.md | Program.cs startup |
| Always use `DateTime.UtcNow` — never `DateTime.Now` | CLAUDE.md | All entity timestamps |
| Dispose ImageSharp with `using`; filter `pixel.A < 128` | CLAUDE.md | Phase 3 only — not relevant to Phase 1 |
| Background sync: `BackgroundService` + `Channel<SyncJob>` | CLAUDE.md | Phase 3 only |

---

## Sources

### Primary (HIGH confidence)
- [learn.microsoft.com — Host ASP.NET Core in a Windows Service (updated 2026-04-29)](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service) — SDK choice, `AddWindowsService()`, content root, service install scripts
- [learn.microsoft.com — Create Windows Service using BackgroundService (updated 2026-03-30)](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service) — `sc.exe` patterns, single-file publish, csproj
- [learn.microsoft.com — Applying EF Core Migrations (updated 2026-04-16)](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) — `MigrateAsync()` startup pattern, migration locking
- [learn.microsoft.com — SQLite Provider Limitations (updated 2026-04-16)](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations) — `__EFMigrationsLock` abandoned lock handling, official `DELETE` workaround
- [learn.microsoft.com — Static files in ASP.NET Core (updated 2026-01-05)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files) — `UseDefaultFiles` + `UseStaticFiles` pattern; confirmed MapStaticAssets does NOT support default documents
- [nuget.org registry — verified 2026-04-30](https://www.nuget.org) — all package versions confirmed via `dotnet package search`

### Secondary (MEDIUM confidence)
- [github.com/serilog/serilog-aspnetcore README](https://github.com/serilog/serilog-aspnetcore/blob/main/README.md) — `UseSerilog()`, rolling file sink parameters

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all package versions verified against nuget.org 2026-04-30
- Architecture: HIGH — based on official Microsoft docs updated within the past 2 months
- Pitfalls: HIGH — stale lock and working-directory pitfalls directly cited in official docs
- Seeding pattern: HIGH — custom `SeedAsync()` after `MigrateAsync()` is the well-established approach for single-record seeding

**Research date:** 2026-04-30
**Valid until:** 2026-05-30 (stable stack; EF Core / ASP.NET Core patch releases are non-breaking)
