---
phase: 04-refactor-project-structure
plan: "03"
subsystem: service-layer
tags: [refactor, controllers, services, layered-architecture, minimal-api-to-mvc]
dependency_graph:
  requires: [FilamentCatalog.EntityFramework, FilamentCatalog.Service]
  provides: [service-interfaces, api-controllers, domain-exceptions]
  affects: [FilamentCatalog.Service/Program.cs]
tech_stack:
  added: []
  patterns: [layered architecture, [ApiController] MVC controllers, service interface pattern, domain exceptions, constructor injection]
key_files:
  created:
    - FilamentCatalog.Service/Models/Exceptions/NotFoundException.cs
    - FilamentCatalog.Service/Models/Exceptions/DomainValidationException.cs
    - FilamentCatalog.Service/Models/Exceptions/ConflictException.cs
    - FilamentCatalog.Service/Models/Dtos/SummaryDto.cs
    - FilamentCatalog.Service/Models/Dtos/BalanceRowDto.cs
    - FilamentCatalog.Service/Models/Requests/OwnerCreateRequest.cs
    - FilamentCatalog.Service/Models/Requests/SpoolCreateRequest.cs
    - FilamentCatalog.Service/Models/Requests/SpoolUpdateRequest.cs
    - FilamentCatalog.Service/Services/IOwnerService.cs
    - FilamentCatalog.Service/Services/OwnerService.cs
    - FilamentCatalog.Service/Services/ISpoolService.cs
    - FilamentCatalog.Service/Services/SpoolService.cs
    - FilamentCatalog.Service/Services/ISummaryService.cs
    - FilamentCatalog.Service/Services/SummaryService.cs
    - FilamentCatalog.Service/Controllers/OwnersController.cs
    - FilamentCatalog.Service/Controllers/SpoolsController.cs
    - FilamentCatalog.Service/Controllers/SummaryController.cs
    - FilamentCatalog.Service/Controllers/BalanceController.cs
  modified:
    - FilamentCatalog.Service/Program.cs
decisions:
  - "Replaced ConfigureHttpJsonOptions with AddControllers().AddJsonOptions() — controllers use the MVC JSON pipeline, not the minimal API pipeline"
  - "Inline record definitions (OwnerCreateRequest, SpoolCreateRequest, SpoolUpdateRequest) removed from Program.cs bottom; they now live in Models/Requests/ files"
  - "Business logic extracted verbatim from Program.cs lambdas into service implementations — no behaviour changes"
metrics:
  duration: "~5 minutes"
  completed: "2026-05-01"
  tasks_completed: 2
  files_created: 18
  files_modified: 1
---

# Phase 4 Plan 3: Service Layer + [ApiController] Controllers Summary

Extracted all inline endpoint lambdas from Program.cs into a proper layered architecture: 3 service interfaces with AppDbContext-injected implementations (OwnerService, SpoolService, SummaryService), 4 [ApiController] MVC controllers (OwnersController, SpoolsController, SummaryController, BalanceController), domain exception types, DTOs, and request records. Program.cs reduced to pure DI bootstrapping with MapControllers(). Solution builds with 0 errors.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create domain exceptions, DTOs, request records, and service layer | d010390 | 14 files created in Models/ and Services/ |
| 2 | Create controllers and rewrite Program.cs | fb87e0f | 4 controllers created, Program.cs rewritten |

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None - this plan is a pure structural refactor. All input validation, auth guards (IsMe check, owner existence), and error response shapes are preserved verbatim from the original Program.cs lambdas. No new network endpoints, auth paths, file access patterns, or schema changes were introduced.

## Self-Check: PASSED

- All 14 service layer files exist in Models/ and Services/
- All 4 controller files exist in Controllers/
- Program.cs contains `app.MapControllers()` and no `MapGet`/`MapPost`/`MapPut`/`MapDelete`
- Program.cs contains `AddControllers().AddJsonOptions(...)` (not `ConfigureHttpJsonOptions`)
- Program.cs contains `UseDefaultFiles()` before `UseStaticFiles()` per CLAUDE.md
- Both task commits (d010390, fb87e0f) verified in git log
- `dotnet build FilamentCatalog.slnx` returns "Build succeeded." with 0 errors, 0 warnings
