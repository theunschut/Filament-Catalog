---
phase: 01-foundation
reviewed: 2026-04-30T00:00:00Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - FilamentCatalog/FilamentCatalog.csproj
  - FilamentCatalog/appsettings.json
  - FilamentCatalog/wwwroot/index.html
  - FilamentCatalog/Models/Owner.cs
  - FilamentCatalog/AppDbContext.cs
  - FilamentCatalog/Program.cs
  - FilamentCatalog/Migrations/20260430215538_InitialCreate.cs
  - FilamentCatalog/Migrations/20260430215538_InitialCreate.Designer.cs
  - FilamentCatalog/Migrations/AppDbContextModelSnapshot.cs
  - install.ps1
  - uninstall.ps1
findings:
  critical: 1
  warning: 5
  info: 3
  total: 9
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-04-30T00:00:00Z
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

Phase 1 establishes the project skeleton: ASP.NET Core 10 Windows service, EF Core + SQLite, the `Owner` model, migrations, and install/uninstall scripts. The overall structure follows CLAUDE.md conventions well — `AppContext.BaseDirectory` for the DB path, `MigrateAsync()` on startup, `UseDefaultFiles()` before `UseStaticFiles()`, and `DateTime.UtcNow` in the seed. One critical issue exists in the startup migration-lock guard (bare catch silently swallowing real database failures). Several warnings relate to missing model constraints, a misleading uninstall message, and a privilege concern in install.ps1.

---

## Critical Issues

### CR-01: Bare `catch` in `ClearStaleEfMigrationsLock` silently swallows real database errors

**File:** `FilamentCatalog/Program.cs:57-64`
**Issue:** The catch block captures every possible exception, including genuine connection failures, corrupted-database errors, and out-of-memory conditions. The comment says the table not existing on first run is "expected and safe to ignore," which is correct — but a `Microsoft.Data.Sqlite.SqliteException` with error code `SQLITE_CORRUPT` or a `InvalidOperationException` because the connection string is wrong would also be swallowed here. The application then proceeds to call `MigrateAsync()` and `SeedAsync()` on a broken context, producing a confusing failure with no log entry explaining the root cause.

**Fix:** Catch only the specific exception that `__EFMigrationsLock` not existing produces. On EF Core + SQLite this is a `Microsoft.Data.Sqlite.SqliteException`. Check `ex.SqliteErrorCode == 1` (SQLITE_ERROR, i.e., "no such table"), or alternatively catch `Exception` but log it and only suppress if the message indicates the table is absent:

```csharp
static async Task ClearStaleEfMigrationsLock(AppDbContext db)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsLock\"");
    }
    catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
    {
        // SQLITE_ERROR: table does not exist — expected on first run, safe to ignore
    }
}
```

---

## Warnings

### WR-01: `Owner.CreatedAt` has no database-level default — silent `0001-01-01` on bad inserts

**File:** `FilamentCatalog/Models/Owner.cs:6`
**Issue:** `CreatedAt` is a non-nullable `DateTime` with no default value configured at the EF Core or SQLite level. The seed in `Program.cs` sets it correctly, but any future code path that constructs and saves an `Owner` without explicitly assigning `CreatedAt` will silently store `0001-01-01T00:00:00`. This is particularly easy to miss when writing minimal-API POST handlers later in the project.

**Fix:** Add a database-level default in `AppDbContext.OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Owner>()
        .Property(o => o.CreatedAt)
        .HasDefaultValueSql("(strftime('%Y-%m-%dT%H:%M:%fZ','now'))");
}
```

Or set the default in the model constructor / property initializer:

```csharp
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```

### WR-02: `Owner.Name` has no maximum-length constraint

**File:** `FilamentCatalog/Models/Owner.cs:4`
**Issue:** `Name` maps to `TEXT` with no length restriction. SQLite itself does not enforce column lengths, so any string — including multi-megabyte values — will be stored without error. When API endpoints are added in Phase 2, unbounded input on `Name` will reach the database with no validation layer.

**Fix:** Add `[MaxLength]` now so the constraint is captured in the migration and surfaces as a model-validation error when the attribute is used with `[ApiController]`:

```csharp
[MaxLength(200)]
public required string Name { get; set; }
```

### WR-03: `install.ps1` registers the service as `LocalSystem` (highest privilege)

**File:** `install.ps1:13-18`
**Issue:** `New-Service` without `-Credential` defaults to the `LocalSystem` account. A local HTTP server that reads/writes a single SQLite file has no need for `LocalSystem`. Running as `LocalSystem` means a bug or future RCE vulnerability (e.g., from an unvalidated file path in the scraper) executes with full system privileges.

**Fix:** Create a dedicated low-privilege account, or use the built-in `NT AUTHORITY\LOCAL SERVICE` / `NT AUTHORITY\NETWORK SERVICE`:

```powershell
New-Service -Name "FilamentCatalog" `
            -BinaryPathName $exePath `
            -DisplayName "Filament Catalog" `
            -Description "Local filament spool tracker" `
            -StartupType Automatic `
            -Credential "NT AUTHORITY\LOCAL SERVICE"
```

Note that `LOCAL SERVICE` will need read/write access to `$PublishDir` — grant it explicitly after creating the service.

### WR-04: `install.ps1` does not check for a pre-existing service

**File:** `install.ps1:13`
**Issue:** If the service already exists (e.g., re-running the script after a failed install), `New-Service` throws a terminating error. The script exits with a confusing PowerShell exception rather than an actionable message. The subsequent `Start-Service` call is never reached.

**Fix:** Guard with a service-existence check before calling `New-Service`:

```powershell
$existing = Get-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Error "Service 'FilamentCatalog' already exists. Run uninstall.ps1 first."
    exit 1
}
```

### WR-05: `uninstall.ps1` prints "Service removed." before the service is actually gone

**File:** `uninstall.ps1:5-6`
**Issue:** `sc.exe delete` on a service that has open handles (e.g., because `Stop-Service` finished but the process hasn't exited yet) marks the service for deletion but does not remove it immediately. The SCM removes it only when the last handle is closed. The immediately following `Write-Host "Service removed."` is therefore misleading — the service entry may still appear in the SCM and re-installing immediately afterward will fail.

**Fix:** Wait for the service entry to disappear and report the deferred case:

```powershell
Stop-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue
sc.exe delete FilamentCatalog | Out-Null

# Wait up to 10 s for the SCM entry to disappear
$waited = 0
while ((Get-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue) -and $waited -lt 10) {
    Start-Sleep -Seconds 1
    $waited++
}

if (Get-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue) {
    Write-Warning "Service marked for deletion but not yet removed (handle still open). Reboot if re-install fails."
} else {
    Write-Host "Service removed."
}
```

---

## Info

### IN-01: Serilog bootstrap logger has no console sink — silent failure if log directory is unwritable

**File:** `FilamentCatalog/Program.cs:5-14`
**Issue:** The bootstrap `Log.Logger` writes only to a rolling file. If the `logs/` subdirectory cannot be created (e.g., permission denied on the publish directory), Serilog's `WriteTo.File` will silently degrade and the fatal-exception catch at line 48 may produce no visible output at all. In an interactive install scenario this makes startup failures very hard to diagnose.

**Fix:** Add a console sink to the bootstrap logger so errors surface during interactive use, and conditionally omit it when running as a Windows service:

```csharp
.WriteTo.Console()
.WriteTo.File(...)
```

Or at minimum ensure the `logs/` directory is created before configuring Serilog:

```csharp
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
```

### IN-02: `appsettings.json` has no `AllowedHosts` restriction

**File:** `FilamentCatalog/appsettings.json`
**Issue:** The ASP.NET Core host-filtering middleware defaults to `AllowedHosts: *` when the key is absent, meaning the service will respond to requests with any `Host` header. CLAUDE.md documents this as a local-only service on `localhost:5000`. A missing `AllowedHosts` is a minor defense-in-depth gap — requests from other machines (or DNS rebinding attacks on a LAN) that reach port 5000 would not be blocked at the host-filtering layer.

**Fix:** Add to `appsettings.json`:

```json
"AllowedHosts": "localhost"
```

### IN-03: `Owner` model file lacks a namespace declaration

**File:** `FilamentCatalog/Models/Owner.cs:1`
**Issue:** The class is declared in the global namespace. While implicit global usings make this compile today, it is inconsistent with the `FilamentCatalog.Migrations` namespace used in the migration files and will cause friction if the project ever references external libraries with a type also named `Owner`.

**Fix:** Add a namespace declaration matching the project assembly name:

```csharp
namespace FilamentCatalog.Models;

public class Owner
{
    ...
}
```

Update `AppDbContext.cs` with the corresponding `using FilamentCatalog.Models;`.

---

_Reviewed: 2026-04-30T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
