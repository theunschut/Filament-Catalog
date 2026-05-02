---
phase: 03-bambu-catalog-sync
plan: "04"
subsystem: frontend
tags: [javascript, es-modules, catalog-picker, sync-ui]
dependency_graph:
  requires: [03-01]
  provides: [catalog-picker-ui, sync-api-wrappers]
  affects: [FilamentCatalog.Service/wwwroot/js/api.js, FilamentCatalog.Service/wwwroot/js/catalog.js, FilamentCatalog.Service/wwwroot/index.html]
tech_stack:
  added: []
  patterns: [ES modules, two-step dependent select, textContent XSS safety]
key_files:
  created:
    - FilamentCatalog.Service/wwwroot/js/catalog.js
  modified:
    - FilamentCatalog.Service/wwwroot/js/api.js
    - FilamentCatalog.Service/wwwroot/index.html
decisions:
  - "Two-step selects placed before name field in dialog so catalog selection auto-fills name"
  - "colorSelect disabled on initial render and after material change until API response returns"
  - "restoreCatalogSelectsFromSpool does not overwrite name/colorHex — populateFormForEdit in spools.js owns those values"
metrics:
  duration: "~10 minutes"
  completed: "2026-05-03"
  tasks_completed: 3
  files_changed: 3
---

# Phase 03 Plan 04: Frontend Sync UI and Catalog Picker Summary

Pure frontend plan: new API fetch wrappers in api.js, new catalog.js ES module with two-step material/color picker, and index.html updated with sync button, last-synced stat, empty-catalog banner, and two-step selects replacing the free-text material input.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add sync and catalog API wrappers to api.js | 3f1eeb8 | api.js |
| 2 | Create catalog.js two-step picker module | a2fcfe5 | catalog.js (new) |
| 3 | Update index.html with sync UI and catalog selects | 6857704 | index.html |

## What Was Built

**api.js extensions (5 new exports):**
- `startSync()` — POST /api/sync/start, expects 202
- `getSyncStatus()` — GET /api/sync/status, returns sync state object
- `getCatalogCount()` — GET /api/catalog/count, returns `{ count: number }`
- `getCatalogMaterials()` — GET /api/catalog/materials, returns `string[]`
- `getCatalogColors(material)` — GET /api/catalog/colors?material=..., returns color array; `encodeURIComponent` applied to material param

**catalog.js (new ES module, 3 exports):**
- `initializeCatalogSelects()` — populates material select from API, resets color select; called on dialog open (add/duplicate)
- `resetCatalogSelects()` — resets both selects to blank placeholder state
- `restoreCatalogSelectsFromSpool(spool)` — re-populates material select, loads colors for spool.material, matches color by `"productTitle — colorName"` pattern; does NOT overwrite saved name/colorHex

Material change handler rebuilds color select (colorSelect disabled during load). Color change handler auto-fills name as `"${productTitle} — ${colorName}"` and syncs colorHex/colorPicker/colorSwatch.

**index.html changes:**
- Sync button (`#sync-catalog-btn`, `class="btn-secondary"`) and last-synced stat (`#stat-last-synced`, initial text "Never") added inside `.summary-left`
- Catalog-empty banner (`#catalog-empty-notice`, `class="info-banner"`, `hidden`) inserted before Add Spool button in filter bar
- Free-text `#spool-material` input removed; replaced with `#spool-catalog-material` select and `#spool-catalog-color` select (disabled until material chosen), both inserted before the name field

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

- `#stat-last-synced` displays "Never" as a static placeholder — will be wired to live sync status data in Plan 05 (app.js / sync polling integration).
- `#catalog-empty-notice` is always hidden — visibility logic tied to catalog count will be implemented in Plan 05.
- `#sync-catalog-btn` has no click handler — Plan 05 will attach the startSync call and polling loop.

These stubs are intentional: this plan is purely markup/JS module structure. Plan 05 wires catalog.js into spools.js and Plan 06 wires the sync button into app.js.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| T-03-11 mitigated | catalog.js | All option text set via `opt.textContent` — no innerHTML with API data |
| T-03-12 mitigated | catalog.js | Name auto-fill uses `.value` property assignment — no XSS vector |

## Self-Check: PASSED

- FilamentCatalog.Service/wwwroot/js/api.js — FOUND, contains startSync + 4 other new exports
- FilamentCatalog.Service/wwwroot/js/catalog.js — FOUND, contains all 3 required exports
- FilamentCatalog.Service/wwwroot/index.html — FOUND, contains spool-catalog-material, sync-catalog-btn, stat-last-synced, catalog-empty-notice; does NOT contain id="spool-material"
- Commits 3f1eeb8, a2fcfe5, 6857704 — all present in git log
