---
phase: 03-bambu-catalog-sync
plan: 02
subsystem: sync-pipeline
tags: [sync-service, background-service, imagesharp, shopify, channel, thread-safety]
dependency_graph:
  requires: [BambuProduct-entity, BambuProducts-table, AppDbContext-BambuProducts]
  provides: [ISyncService, SyncService, SyncStateService, SyncBackgroundService, SyncStatusDto, SyncJob]
  affects: [FilamentCatalog.Service]
tech_stack:
  added: []
  patterns: [BackgroundService-Channel-consumer, lock-based-thread-safety, cursor-pagination, ImageSharp-ProcessPixelRows, EF-upsert-pattern]
key_files:
  created:
    - FilamentCatalog.Service/Models/Dtos/SyncStatusDto.cs
    - FilamentCatalog.Service/Services/SyncStateService.cs
    - FilamentCatalog.Service/Services/ISyncService.cs
    - FilamentCatalog.Service/Services/SyncService.cs
    - FilamentCatalog.Service/Services/SyncBackgroundService.cs
  modified: []
decisions:
  - "ImageSharp 3.x uses ProcessPixelRows(accessor) not GetPixelMemoryGroup() — auto-fixed during Task 2"
  - "SyncJob record defined in SyncBackgroundService.cs (not SyncService.cs) to avoid duplicate type definition when Program.cs creates Channel<SyncJob>"
  - "stateService.Start() called twice: immediately (mark running) then after FetchAll (set accurate TotalEstimate) — intentional design per plan"
  - "HttpClient injected via DI to avoid socket exhaustion — not newed inside method"
  - "ex.Message only (not ToString()) surfaced in SyncStateService.Error() per threat model T-03-05"
metrics:
  duration: ~5 minutes
  completed: 2026-05-03
  tasks_completed: 3
  files_modified: 5
---

# Phase 3 Plan 2: Sync Pipeline Implementation Summary

**One-liner:** Background sync pipeline with Shopify cursor pagination, ImageSharp ProcessPixelRows color extraction, EF upsert on Name+Material, and thread-safe SyncStateService progress tracking.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Create SyncStatusDto DTO and SyncStateService singleton | 6b93a23 |
| 2 | Create ISyncService interface and SyncService implementation | 7050542 |
| 3 | Create SyncJob record and SyncBackgroundService | a12cec3 |

## What Was Built

- `SyncStatusDto`: DTO with Status (idle/running/completed/error), ProcessedCount, TotalEstimate, PercentComplete (computed property), ErrorMessage, LastSyncedAt
- `SyncStateService`: Thread-safe singleton using `lock` object; exposes GetStatus(), Start(totalEstimate), IncrementProgress(), Complete(syncTime), Error(message)
- `ISyncService`: Single-method interface `Task SyncCatalogAsync(CancellationToken)`
- `SyncService`: Full implementation — Shopify `/products.json` cursor pagination via Link header, per-variant material/color option parsing, swatch image download with 10s timeout, ImageSharp center-crop + alpha<128 dominant color extraction, EF upsert on Name+Material composite key
- `SyncBackgroundService`: Extends BackgroundService; consumes `Channel<SyncJob>` via `ReadAllAsync(stoppingToken)`; delegates to ISyncService; swallows OperationCanceledException on service stop
- `SyncJob`: Public record defined in SyncBackgroundService.cs for Program.cs channel creation

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed ImageSharp 3.x pixel enumeration API**
- **Found during:** Task 2 (build verification)
- **Issue:** Plan code used `image.GetPixelMemoryGroup()` which does not exist in SixLabors.ImageSharp 3.x. Compiler error CS1061.
- **Fix:** Replaced with `image.ProcessPixelRows(accessor => { ... })` which is the correct ImageSharp 3.x API for pixel enumeration. Inner logic (alpha<128 filter, RGB accumulation) unchanged.
- **Files modified:** `FilamentCatalog.Service/Services/SyncService.cs`
- **Commit:** 7050542

## Verification

- `dotnet build "FilamentCatalog.Service/FilamentCatalog.Service.csproj"` exits 0 (0 warnings, 0 errors)
- All 5 files created and compile cleanly
- `SyncService.cs` contains `pixel.A < 128` (alpha filter per CLAUDE.md)
- `SyncService.cs` contains `image.Mutate(ctx => ctx.Crop` (center crop per CLAUDE.md)
- `SyncService.cs` contains `DateTime.UtcNow` only (no DateTime.Now)
- `SyncService.cs` contains `using var image = Image.Load<Rgba32>` (disposed per CLAUDE.md)
- `SyncStateService.cs` contains `private readonly object _lock = new()` (thread-safe)

## Known Stubs

None — this plan creates service layer only. No DI wiring, no controllers, no frontend. Data pipeline is complete and correct; integration wiring is Plan 03-03.

## Threat Flags

None — threat model dispositions all addressed:
- T-03-02 (per-image 10s timeout via CancellationTokenSource.CreateLinkedTokenSource + CancelAfter): implemented
- T-03-03 (HttpClient injected, not newed): implemented
- T-03-05 (ex.Message only, not ToString() in Error()): implemented

## Self-Check: PASSED

- FilamentCatalog.Service/Models/Dtos/SyncStatusDto.cs: FOUND
- FilamentCatalog.Service/Services/SyncStateService.cs: FOUND
- FilamentCatalog.Service/Services/ISyncService.cs: FOUND
- FilamentCatalog.Service/Services/SyncService.cs: FOUND
- FilamentCatalog.Service/Services/SyncBackgroundService.cs: FOUND
- Commit 6b93a23: FOUND
- Commit 7050542: FOUND
- Commit a12cec3: FOUND
