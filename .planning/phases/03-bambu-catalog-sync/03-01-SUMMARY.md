---
phase: 03-bambu-catalog-sync
plan: 01
subsystem: data-layer
tags: [entity-framework, migration, sqlite, imagesharp, bambu-catalog]
dependency_graph:
  requires: []
  provides: [BambuProduct-entity, BambuProducts-table, AppDbContext-BambuProducts]
  affects: [FilamentCatalog.EntityFramework, FilamentCatalog.Service]
tech_stack:
  added: [SixLabors.ImageSharp@3.1.12]
  patterns: [EF-Core-composite-unique-index, implicit-global-namespace]
key_files:
  created:
    - FilamentCatalog.EntityFramework/Models/BambuProduct.cs
    - FilamentCatalog.EntityFramework/Migrations/20260502232428_AddBambuProduct.cs
    - FilamentCatalog.EntityFramework/Migrations/20260502232428_AddBambuProduct.Designer.cs
  modified:
    - FilamentCatalog.EntityFramework/AppDbContext.cs
    - FilamentCatalog.EntityFramework/Migrations/AppDbContextModelSnapshot.cs
    - FilamentCatalog.Service/FilamentCatalog.Service.csproj
decisions:
  - "ImageSharp added to FilamentCatalog.Service (not EntityFramework) since SyncService lives in Service project"
  - "Microsoft.EntityFrameworkCore.Design added to Service project (PrivateAssets=all) to enable dotnet ef --startup-project"
  - "Composite unique index on (Name, Material) via HasIndex().IsUnique() — EF generates IX not AK constraint"
metrics:
  duration: ~8 minutes
  completed: 2026-05-03
  tasks_completed: 3
  files_modified: 6
---

# Phase 3 Plan 1: BambuProduct Entity and Migration Summary

**One-liner:** BambuProduct entity with 7 fields, EF Core migration creating BambuProducts table with composite unique index on (Name, Material), and ImageSharp 3.1.12 added to Service project.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Add SixLabors.ImageSharp 3.1.12 to FilamentCatalog.Service.csproj | 5e0d243 |
| 2 | Create BambuProduct entity and update AppDbContext | 1457e31 |
| 3 | Generate EF migration AddBambuProduct | 0779876 |

## What Was Built

- `BambuProduct` entity with implicit global namespace (consistent with Spool, Owner models): Id, Name, Material, ColorName, ColorHex, ColorSwatchUrl (nullable), LastSyncedAt
- `AppDbContext.BambuProducts` DbSet with three index configurations: unique (Name, Material), Material, LastSyncedAt
- EF Core migration `20260502232428_AddBambuProduct` creating the BambuProducts table with all columns and indexes
- ImageSharp 3.1.12 NuGet package in FilamentCatalog.Service (where SyncService will live in Plan 03-02)
- `Microsoft.EntityFrameworkCore.Design` added to Service project (PrivateAssets=all, development-only dependency required for `dotnet ef` tooling)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Microsoft.EntityFrameworkCore.Design to Service project**
- **Found during:** Task 3 (EF migration generation)
- **Issue:** `dotnet ef migrations add ... --startup-project "FilamentCatalog.Service"` failed with "startup project doesn't reference Microsoft.EntityFrameworkCore.Design". The Design package was in the EF project but `dotnet ef` requires it in the startup project.
- **Fix:** Added `Microsoft.EntityFrameworkCore.Design` 10.0.7 to FilamentCatalog.Service.csproj with `PrivateAssets=all` (development tooling only, not deployed)
- **Files modified:** `FilamentCatalog.Service/FilamentCatalog.Service.csproj`
- **Commit:** 0779876

## Verification

- `dotnet build "FilamentCatalog.Service/FilamentCatalog.Service.csproj"` exits 0
- Migration file `20260502232428_AddBambuProduct.cs` contains `CreateTable(name: "BambuProducts")`
- Migration contains unique index `IX_BambuProducts_Name_Material` with `unique: true`
- Migration contains indexes on `Material` and `LastSyncedAt`
- AppDbContextModelSnapshot.cs updated with BambuProducts table definition

## Known Stubs

None — this plan creates infrastructure only (entity, migration). No data wiring or UI yet.

## Threat Flags

None — all threat model dispositions are `accept` per plan frontmatter. BambuProduct stores no PII; ColorSwatchUrl stores public Shopify CDN URLs only.

## Self-Check: PASSED

- FilamentCatalog.EntityFramework/Models/BambuProduct.cs: FOUND
- FilamentCatalog.EntityFramework/AppDbContext.cs (contains DbSet<BambuProduct>): FOUND
- FilamentCatalog.EntityFramework/Migrations/20260502232428_AddBambuProduct.cs: FOUND
- FilamentCatalog.Service/FilamentCatalog.Service.csproj (contains SixLabors.ImageSharp): FOUND
- Commit 5e0d243: FOUND
- Commit 1457e31: FOUND
- Commit 0779876: FOUND
