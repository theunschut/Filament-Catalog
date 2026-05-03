---
phase: 03-bambu-catalog-sync
plan: "03"
subsystem: backend-api
tags: [controllers, sync, catalog, di-wiring]
dependency_graph:
  requires: [03-02]
  provides: [sync-api, catalog-api, di-complete]
  affects: [FilamentCatalog.Service]
tech_stack:
  added: []
  patterns: [channel-capacity-guard, ef-core-parameterized-query, typed-httpclient]
key_files:
  created:
    - FilamentCatalog.Service/Controllers/SyncController.cs
    - FilamentCatalog.Service/Controllers/CatalogController.cs
  modified:
    - FilamentCatalog.Service/Program.cs
decisions:
  - "Explicit Route('api/sync') used on SyncController to avoid casing mismatch with JS fetch calls"
  - "CatalogController uses AppDbContext directly (no service layer) for simple projection queries"
  - "Channel<SyncJob> registered as singleton before SyncBackgroundService to satisfy constructor injection"
metrics:
  duration: "~5 minutes"
  completed: "2026-05-03"
  tasks_completed: 3
  tasks_total: 3
---

# Phase 03 Plan 03: API Controllers + DI Wiring Summary

Wire the sync pipeline into ASP.NET Core: SyncController (202 start + status polling), CatalogController (count/materials/colors catalog queries), and Program.cs DI registrations completing the backend.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create SyncController | 26ed9a0 | FilamentCatalog.Service/Controllers/SyncController.cs |
| 2 | Create CatalogController | 90f1b1d | FilamentCatalog.Service/Controllers/CatalogController.cs |
| 3 | Wire sync DI services in Program.cs | 88f510a | FilamentCatalog.Service/Program.cs |

## What Was Built

### SyncController (`/api/sync`)

- `POST /api/sync/start` — enqueues `SyncJob` via `channel.Writer.WriteAsync`, returns `202 Accepted`
- `GET /api/sync/status` — delegates to `SyncStateService.GetStatus()`, returns `SyncStatusDto` with status, processedCount, totalEstimate, errorMessage, lastSyncedAt

### CatalogController (`/api/catalog`)

- `GET /api/catalog/count` — returns `{ count: N }` from BambuProducts table
- `GET /api/catalog/materials` — returns distinct material strings sorted alphabetically
- `GET /api/catalog/colors?material=` — returns `[{ id, colorName, colorHex, productTitle }]` filtered by material; validates query param, uses EF Core LINQ (parameterized SQL, no injection risk)

### Program.cs DI Registrations Added

```csharp
var syncChannel = Channel.CreateBounded<SyncJob>(
    new BoundedChannelOptions(capacity: 1) { FullMode = BoundedChannelFullMode.DropNewest });
builder.Services.AddSingleton(syncChannel);
builder.Services.AddSingleton<SyncStateService>();
builder.Services.AddHttpClient<SyncService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddHostedService<SyncBackgroundService>();
```

## Deviations from Plan

None - plan executed exactly as written.

## Threat Model Compliance

| Threat ID | Mitigation | Status |
|-----------|-----------|--------|
| T-03-07 | POST /api/sync/start takes no body; SyncJob is fixed-schema | Applied |
| T-03-08 | Channel capacity 1 + DropNewest prevents DoS via repeated sync requests | Applied via Channel.CreateBounded |
| T-03-09 | material parameter passed to EF Core LINQ .Where() — parameterized query | Applied |
| T-03-10 | SyncStateService.Error() stores ex.Message only (no stack trace) | Already in SyncService from 03-02 |

## Known Stubs

None — all endpoints query real database tables. Data will be empty until sync runs (expected pre-sync state).

## Threat Flags

None — no new security surface beyond what was planned.

## Self-Check: PASSED

- FilamentCatalog.Service/Controllers/SyncController.cs: FOUND
- FilamentCatalog.Service/Controllers/CatalogController.cs: FOUND
- FilamentCatalog.Service/Program.cs: modified (AddHostedService<SyncBackgroundService> present)
- Commits 26ed9a0, 90f1b1d, 88f510a: all present in git log
- Build: succeeded with 0 warnings, 0 errors
