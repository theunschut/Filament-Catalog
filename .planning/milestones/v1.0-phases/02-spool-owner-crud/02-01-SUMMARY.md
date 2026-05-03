---
phase: 02-spool-owner-crud
plan: 01
subsystem: database
tags: [efcore, sqlite, migrations, csharp, entity]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: AppDbContext base class, Owner model, EF Core migrations pattern, SQLite setup
provides:
  - Spool entity class with all D-02 fields
  - PaymentStatus enum (Paid/Unpaid/Partial)
  - SpoolStatus enum (Sealed/Active/Empty)
  - AppDbContext extended with DbSet<Spool> and DeleteBehavior.Restrict FK config
  - EF Core migration AddSpools creating Spools table in SQLite
affects: [02-02-api-endpoints, 02-03-ui, 02-04-balance]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - EF Core Fluent API for FK with DeleteBehavior.Restrict
    - Enum fields as C# types with default values on entity

key-files:
  created:
    - FilamentCatalog/Models/Spool.cs
    - FilamentCatalog/Models/PaymentStatus.cs
    - FilamentCatalog/Models/SpoolStatus.cs
    - FilamentCatalog/Migrations/20260501070939_AddSpools.cs
    - FilamentCatalog/Migrations/20260501070939_AddSpools.Designer.cs
  modified:
    - FilamentCatalog/AppDbContext.cs
    - FilamentCatalog/Migrations/AppDbContextModelSnapshot.cs

key-decisions:
  - "DeleteBehavior.Restrict on Spool->Owner FK — EF Core default is Cascade; explicit Restrict enforces D-03 (409 on owner delete with spools)"
  - "Enum default values set on entity (PaymentStatus.Unpaid, SpoolStatus.Sealed) — D-02 field defaults applied at C# level"
  - "OwnerId declared as explicit property (not shadow) — required for request body binding in Plan 02 API"

patterns-established:
  - "No namespace declarations in model files — matches Owner.cs implicit namespace convention"
  - "Expression-body DbSet properties — matches existing AppDbContext style"
  - "OnModelCreating override for FK configuration — EF Core Fluent API pattern"

requirements-completed: [SPOOL-01, SPOOL-02, SPOOL-03]

# Metrics
duration: 8min
completed: 2026-05-01
---

# Phase 2 Plan 01: Spool Entity, Enums, and AddSpools Migration Summary

**Spool EF Core entity with PaymentStatus/SpoolStatus enums, DeleteBehavior.Restrict FK, and generated SQLite migration creating the Spools table**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-05-01
- **Completed:** 2026-05-01
- **Tasks:** 2
- **Files modified:** 7 (3 created models, 1 updated context, 3 migration files)

## Accomplishments
- Created `Spool.cs` with all 13 D-02 fields: Id, Name, Material, ColorHex, OwnerId, Owner (nav), WeightGrams, PricePaid, PaymentStatus, SpoolStatus, Notes, CreatedAt
- Created `PaymentStatus` and `SpoolStatus` enums matching plan spec exactly
- Extended `AppDbContext` with `DbSet<Spool>` and `OnModelCreating` configuring `DeleteBehavior.Restrict` on the Spool->Owner FK (D-03)
- Generated `20260501070939_AddSpools` migration — creates Spools table with all columns and `ReferentialAction.Restrict` FK constraint

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Spool entity and enum files** - `8610808` (feat)
2. **Task 2: Extend AppDbContext and generate AddSpools migration** - `eaa9c67` (feat)

**Plan metadata:** committed with SUMMARY (docs)

## Files Created/Modified
- `FilamentCatalog/Models/Spool.cs` - Spool entity with all D-02 fields, OwnerId FK, enum defaults
- `FilamentCatalog/Models/PaymentStatus.cs` - Enum: Paid, Unpaid, Partial
- `FilamentCatalog/Models/SpoolStatus.cs` - Enum: Sealed, Active, Empty
- `FilamentCatalog/AppDbContext.cs` - Added DbSet<Spool> and OnModelCreating with DeleteBehavior.Restrict
- `FilamentCatalog/Migrations/20260501070939_AddSpools.cs` - Generated migration: CreateTable Spools + FK Restrict
- `FilamentCatalog/Migrations/20260501070939_AddSpools.Designer.cs` - Generated migration designer file
- `FilamentCatalog/Migrations/AppDbContextModelSnapshot.cs` - Updated model snapshot

## Decisions Made
- `DeleteBehavior.Restrict` was explicitly configured in `OnModelCreating` — EF Core's default for a required FK is `Cascade`, which would violate D-03. The explicit override is verified to propagate to `ReferentialAction.Restrict` in the generated migration.
- `NuGet restore` was required before running `dotnet ef migrations add` since the worktree obj/ directory was clean. This is a normal worktree setup step, not a deviation.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `dotnet ef migrations add AddSpools` initially failed with "Assets file not found" because the worktree `obj/` directory had no `project.assets.json`. Running `dotnet restore` first resolved it. This is expected behavior for a fresh git worktree. No plan change required.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Spool data layer is complete and the project builds cleanly (0 warnings, 0 errors)
- Plan 02 (API endpoints) can now register `GET/POST /api/spools`, `GET/PUT/DELETE /api/spools/{id}`, owner endpoints, and summary/balance endpoints against the `DbSet<Spool>` and FK-configured context
- The `AppDbContextModelSnapshot.cs` is up to date — Plan 02's migration (if any) will diff cleanly against it

---
*Phase: 02-spool-owner-crud*
*Completed: 2026-05-01*
