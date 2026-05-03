---
phase: 02-spool-owner-crud
plan: 02
subsystem: api
tags: [aspnetcore, minimal-api, csharp, crud, endpoints]

# Dependency graph
requires:
  - phase: 02-spool-owner-crud
    plan: 02-01
    provides: Spool entity, PaymentStatus/SpoolStatus enums, AppDbContext with DbSet<Spool>
provides:
  - GET /api/owners — list all owners ordered by CreatedAt
  - POST /api/owners — create owner with 422 guard for empty Name
  - DELETE /api/owners/{id:int} — delete owner with IsMe(422) and spoolCount(409) guards
  - GET /api/spools — list all spools with Owner included, ordered by CreatedAt
  - POST /api/spools — create spool; ColorHex defaults to #888888; DateTime.UtcNow for CreatedAt
  - PUT /api/spools/{id:int} — update all mutable spool fields; 404 if not found
  - DELETE /api/spools/{id:int} — delete spool; 404 if not found
  - GET /api/summary — returns totalSpools/mySpools/totalValue/totalOwed
  - GET /api/balance — returns per-owner owed/value rows excluding Me owner
  - JsonStringEnumConverter configured globally (enums serialize as strings)
affects: [02-04-balance-ui, frontend api.js consumers]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ASP.NET Core minimal API MapGet/MapPost/MapPut/MapDelete pattern
    - ConfigureHttpJsonOptions with JsonStringEnumConverter for global enum string serialization
    - record types for API request bodies (OwnerCreateRequest, SpoolCreateRequest, SpoolUpdateRequest)
    - Results.UnprocessableEntity / Results.Conflict / Results.NotFound / Results.Created / Results.Ok / Results.NoContent

key-files:
  created: []
  modified:
    - FilamentCatalog/Program.cs

key-decisions:
  - "JsonStringEnumConverter added via ConfigureHttpJsonOptions — applies globally to all minimal API responses; enums serialize as Paid/Unpaid/Partial not 0/1/2"
  - "Balance endpoint loads all spools into memory (acceptable for <10,000 rows per A1 assumption in RESEARCH.md)"
  - "ColorHex defaults to #888888 in both POST and PUT — no format validation beyond null/empty check (T-02-02-03 accepted risk)"
  - "OwnerCreateRequest/SpoolCreateRequest/SpoolUpdateRequest as C# records — matches minimal API parameter binding pattern"

# Metrics
duration: 10min
completed: 2026-05-01
---

# Phase 2 Plan 02: API Endpoints Summary

**All 9 minimal API endpoints registered in Program.cs with JsonStringEnumConverter global config, enum string serialization, and Me-owner exclusion in balance/summary**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-05-01
- **Completed:** 2026-05-01
- **Tasks:** 2
- **Files modified:** 1 (Program.cs only)

## Accomplishments

- Added `using System.Text.Json.Serialization` and `ConfigureHttpJsonOptions` with `JsonStringEnumConverter` so all API responses serialize enums as strings (`"Paid"`, `"Unpaid"`, `"Partial"`, `"Sealed"`, `"Active"`, `"Empty"`)
- Registered 3 Owner endpoints: GET (list ordered by CreatedAt), POST (422 on empty Name), DELETE (NotFound/IsMe 422/spoolCount 409 guards)
- Registered 4 Spool endpoints: GET (with Owner include), POST (ColorHex defaults `#888888`, `DateTime.UtcNow` for CreatedAt), PUT (404 + validation guards), DELETE (404 guard)
- Registered GET /api/summary returning `{ totalSpools, mySpools, totalValue, totalOwed }` — totalOwed excludes Me owner's spools
- Registered GET /api/balance returning per-owner rows `{ ownerId, ownerName, spoolCount, value, owed, hasUnpriced }` — Me owner excluded entirely
- `app.UseDefaultFiles()` still precedes `app.UseStaticFiles()` (middleware order preserved)
- All Map* calls appear before `await app.RunAsync()`
- Build: 0 errors, 0 warnings

## Task Commits

Each task was committed atomically:

1. **Task 1: Add enum JSON config and register Owner endpoints** — `e3e44f4` (feat)
2. **Task 2: Register Spool endpoints and summary/balance endpoints** — `7582e47` (feat)

**Plan metadata:** committed with SUMMARY (docs)

## Files Created/Modified

- `FilamentCatalog/Program.cs` — Extended with JsonStringEnumConverter config + all 9 endpoint registrations + 3 request record types

## Decisions Made

- `JsonStringEnumConverter` added globally via `ConfigureHttpJsonOptions` — affects all ASP.NET Core minimal API JSON responses. Per plan spec, this is required for the JS filter logic in Plan 03/04 to work correctly.
- Balance endpoint does not include the Me owner in results — only non-me owners appear. `meOwner` variable removed from balance endpoint body (not needed since we filter with `!o.IsMe` directly in the LINQ query).
- Summary `totalOwed` computation excludes spools owned by the Me owner (as per D-11).

## Deviations from Plan

None - plan executed exactly as written.

## Threat Surface Scan

All threats in the plan's threat register were addressed as designed:

- T-02-02-01: EF Core parameterized queries exclusively — no raw SQL in endpoint handlers
- T-02-02-02: IsMe guard checked before spoolCount guard in DELETE /api/owners/{id}
- T-02-02-03: ColorHex defaults to `#888888` in POST and PUT — accepted cosmetic-only risk
- T-02-02-04: localhost-only, no auth — accepted by design
- T-02-02-05: in-memory spool load for summary/balance — accepted for <10,000 rows
- T-02-02-06: No audit log — accepted for v1 single-user scope

No new threat surface introduced beyond what the plan's threat model covers.

---
*Phase: 02-spool-owner-crud*
*Completed: 2026-05-01*
