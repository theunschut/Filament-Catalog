---
phase: 04-refactor-project-structure
verified: 2026-05-01T00:00:00Z
status: human_needed
score: 10/10 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Run the installed Windows service and exercise all 9 endpoints"
    expected: "GET /api/owners, POST /api/owners, DELETE /api/owners/{id}, GET /api/spools, POST /api/spools, PUT /api/spools/{id}, DELETE /api/spools/{id}, GET /api/summary, GET /api/balance all return expected HTTP status codes and JSON payloads"
    why_human: "dotnet build confirms compilation correctness but cannot verify runtime behavior of MVC controllers — especially the JsonStringEnumConverter (enum strings vs ints), the [ApiController] model-binding behavior, and Windows service host execution"
  - test: "Open http://localhost:5000 in a browser after starting the service"
    expected: "index.html loads and all frontend features (add/edit/delete spool, owner management, summary bar, balance table) continue working without regression after the Program.cs rewrite"
    why_human: "The frontend relies on specific JSON shapes (enum strings like 'Sealed', 'Active', 'Empty', 'Unpaid', 'Paid') via JsonStringEnumConverter. The switch from ConfigureHttpJsonOptions to AddControllers().AddJsonOptions() must be confirmed to produce identical serialization for the existing JS code."
---

# Phase 4: Refactor Project Structure — Verification Report

**Phase Goal:** Split EF Core layer into FilamentCatalog.EntityFramework project, rename main project to FilamentCatalog.Service, and extract API endpoints from Program.cs into organized service/controller classes with proper DI
**Verified:** 2026-05-01
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | FilamentCatalog.EntityFramework project exists with valid .csproj (SDK=Microsoft.NET.Sdk, not Web) | VERIFIED | File read: `<Project Sdk="Microsoft.NET.Sdk">` confirmed; EF SQLite + Design packages at 10.0.7 |
| 2 | All model files (Owner, Spool, PaymentStatus, SpoolStatus) present in new project | VERIFIED | `ls FilamentCatalog.EntityFramework/Models/` shows all 4 files |
| 3 | AppDbContext.cs in new project contains DbSet<Owner> and DbSet<Spool> | VERIFIED | File read: `DbSet<Owner>`, `DbSet<Spool>`, `DeleteBehavior.Restrict` all present |
| 4 | All 5 migration files present in new project's Migrations/ folder | VERIFIED | `ls FilamentCatalog.EntityFramework/Migrations/` shows all 5 files |
| 5 | The solution has two projects in FilamentCatalog.slnx | VERIFIED | FilamentCatalog.slnx contains exactly FilamentCatalog.EntityFramework and FilamentCatalog.Service |
| 6 | FilamentCatalog.Service.csproj references FilamentCatalog.EntityFramework via ProjectReference | VERIFIED | `<ProjectReference Include="..\FilamentCatalog.EntityFramework\FilamentCatalog.EntityFramework.csproj" />` present |
| 7 | FilamentCatalog.Service.csproj has no EF Core package references (no Sqlite, no Design) | VERIFIED | .csproj contains only WindowsServices and Serilog.AspNetCore; EF removed |
| 8 | Duplicate model/migration files removed from FilamentCatalog.Service/ | VERIFIED | AppDbContext.cs NOT present; Migrations/ NOT present; Models/ contains only Dtos/, Exceptions/, Requests/ |
| 9 | Program.cs contains only bootstrapping — no inline MapGet/MapPost/etc.; calls app.MapControllers() | VERIFIED | grep found 0 Map* inline handlers; `app.MapControllers()` present on line 53; 3 AddScoped registrations confirmed |
| 10 | Four [ApiController] controllers + three service interfaces with implementations cover all 9 API routes | VERIFIED | 9 HTTP verb attributes confirmed across 4 controllers; no AppDbContext in controllers; all services use AppDbContext constructor injection |

**Score:** 10/10 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `FilamentCatalog.EntityFramework/FilamentCatalog.EntityFramework.csproj` | EF class library project | VERIFIED | SDK=Microsoft.NET.Sdk, net10.0, EF Sqlite+Design |
| `FilamentCatalog.EntityFramework/AppDbContext.cs` | DbContext with Owner+Spool DbSets | VERIFIED | DbSet<Owner>, DbSet<Spool>, DeleteBehavior.Restrict |
| `FilamentCatalog.EntityFramework/Models/Owner.cs` | Owner entity | VERIFIED | Present |
| `FilamentCatalog.EntityFramework/Models/Spool.cs` | Spool entity | VERIFIED | Present |
| `FilamentCatalog.EntityFramework/Migrations/AppDbContextModelSnapshot.cs` | EF migration snapshot | VERIFIED | Present |
| `FilamentCatalog.slnx` | Solution with both projects | VERIFIED | Exactly 2 Project entries |
| `FilamentCatalog.Service/FilamentCatalog.Service.csproj` | Service project with ProjectReference | VERIFIED | ProjectReference wired, no EF packages |
| `FilamentCatalog.Service/Services/IOwnerService.cs` | Owner service interface | VERIFIED | GetAllAsync, CreateAsync, DeleteAsync |
| `FilamentCatalog.Service/Services/ISpoolService.cs` | Spool service interface | VERIFIED | GetAllAsync, CreateAsync, UpdateAsync, DeleteAsync |
| `FilamentCatalog.Service/Services/ISummaryService.cs` | Summary service interface | VERIFIED | Present |
| `FilamentCatalog.Service/Services/OwnerService.cs` | Owner service implementation | VERIFIED | AppDbContext constructor injection, domain exceptions thrown |
| `FilamentCatalog.Service/Services/SpoolService.cs` | Spool service implementation | VERIFIED | Present |
| `FilamentCatalog.Service/Services/SummaryService.cs` | Summary service implementation | VERIFIED | Present |
| `FilamentCatalog.Service/Controllers/OwnersController.cs` | Owner HTTP endpoints | VERIFIED | [ApiController], IOwnerService injection, GET/POST/DELETE |
| `FilamentCatalog.Service/Controllers/SpoolsController.cs` | Spool HTTP endpoints | VERIFIED | [ApiController], ISpoolService injection, GET/POST/PUT/DELETE |
| `FilamentCatalog.Service/Controllers/SummaryController.cs` | Summary endpoint | VERIFIED | [ApiController], ISummaryService injection |
| `FilamentCatalog.Service/Controllers/BalanceController.cs` | Balance endpoint | VERIFIED | [ApiController], ISummaryService injection |
| `FilamentCatalog.Service/Models/Exceptions/NotFoundException.cs` | Domain exception | VERIFIED | Present |
| `FilamentCatalog.Service/Models/Exceptions/DomainValidationException.cs` | Domain exception | VERIFIED | Present |
| `FilamentCatalog.Service/Models/Exceptions/ConflictException.cs` | Domain exception | VERIFIED | Present |
| `FilamentCatalog.Service/Program.cs` | Pure bootstrapping, MapControllers() | VERIFIED | No inline handlers; UseDefaultFiles before UseStaticFiles preserved |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `FilamentCatalog.Service/FilamentCatalog.Service.csproj` | `FilamentCatalog.EntityFramework/FilamentCatalog.EntityFramework.csproj` | ProjectReference | WIRED | Path `..\FilamentCatalog.EntityFramework\...` confirmed in .csproj |
| `FilamentCatalog.EntityFramework/AppDbContext.cs` | `FilamentCatalog.EntityFramework/Models/` | DbSet<Owner>, DbSet<Spool> type references | WIRED | Both DbSets present, DeleteBehavior.Restrict configured |
| `FilamentCatalog.Service/Controllers/OwnersController.cs` | `FilamentCatalog.Service/Services/IOwnerService.cs` | constructor injection | WIRED | `OwnersController(IOwnerService ownerService)` confirmed |
| `FilamentCatalog.Service/Services/OwnerService.cs` | `FilamentCatalog.EntityFramework/AppDbContext.cs` | constructor injection | WIRED | `OwnerService(AppDbContext db)` confirmed |
| `FilamentCatalog.Service/Program.cs` | service registrations | AddScoped | WIRED | All 3 service interfaces registered as scoped |
| `FilamentCatalog.Service/Program.cs` | controllers | app.MapControllers() | WIRED | Line 53 confirmed |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution compiles | `dotnet build FilamentCatalog.slnx` | Build succeeded. 0 Warning(s). 0 Error(s). | PASS |
| No inline endpoint handlers in Program.cs | `grep MapGet\|MapPost\|MapPut\|MapDelete Program.cs` | NONE FOUND | PASS |
| UseDefaultFiles before UseStaticFiles | line number check | Line 50 UseDefaultFiles, line 51 UseStaticFiles | PASS |
| All 9 routes covered | grep HttpGet/Post/Put/Delete all controllers | 9 verb attributes found across 4 controllers | PASS |
| No AppDbContext in controllers | grep AppDbContext controllers/ | NONE FOUND | PASS |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `FilamentCatalog/` (old dir) | n/a | Leftover directory at solution root (bin/obj only, no source files) | Info | No functional impact — git history artifact from the rename; contains no source files |

No stubs, no TODO/FIXME, no hardcoded empty returns found in any service or controller file.

### Human Verification Required

#### 1. Enum Serialization Regression Test

**Test:** Start the service, call `GET /api/spools` and `GET /api/owners`, inspect the JSON response for enum fields
**Expected:** `paymentStatus` and `spoolStatus` fields serialize as strings (`"Sealed"`, `"Active"`, `"Empty"`, `"Unpaid"`, `"Partial"`, `"Paid"`) — not as integers. The existing frontend JavaScript (filters.js, spools.js) depends on string comparison against these values.
**Why human:** The switch from `ConfigureHttpJsonOptions` (minimal API pipeline) to `AddControllers().AddJsonOptions()` (MVC pipeline) is architecturally correct but the two pipelines are independent. The `JsonStringEnumConverter` is registered in the MVC pipeline; whether it also applies to any remaining minimal API paths (none expected) and whether the existing frontend JS still receives the expected strings must be confirmed by inspecting an actual HTTP response.

#### 2. All 9 Endpoints Functional

**Test:** Exercise all 9 API routes with curl or the browser:
- `GET /api/owners` — expect 200 + JSON array
- `POST /api/owners` `{"name":"Test"}` — expect 201
- `DELETE /api/owners/{id}` — expect 204 (or 409 if spools assigned)
- `GET /api/spools` — expect 200 + JSON array
- `POST /api/spools` (valid body) — expect 201
- `PUT /api/spools/{id}` (valid body) — expect 200
- `DELETE /api/spools/{id}` — expect 204
- `GET /api/summary` — expect 200 + `{totalSpools, mySpools, totalValue, totalOwed}`
- `GET /api/balance` — expect 200 + JSON array of balance rows
**Expected:** All endpoints return correct status codes and JSON shapes matching what the frontend expects.
**Why human:** `dotnet build` verifies compilation only. Runtime behavior of `[ApiController]` model binding, exception-to-status-code mapping, and MVC routing conventions require an actual running instance.

---

### Gaps Summary

No gaps found. All must-haves from the ROADMAP success criteria and PLAN frontmatter are satisfied by the codebase. The phase goal is structurally complete and the build passes clean. Two human verification items remain to confirm runtime correctness, which cannot be validated programmatically without starting the service.

The residual `FilamentCatalog/` directory (containing only `bin/` and `obj/` build artifacts) is a cosmetic leftover from the `git mv` rename and has no functional impact.

---

_Verified: 2026-05-01_
_Verifier: Claude (gsd-verifier)_
