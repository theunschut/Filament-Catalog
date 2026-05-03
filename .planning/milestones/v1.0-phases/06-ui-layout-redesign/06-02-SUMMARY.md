---
phase: 06-ui-layout-redesign
plan: "02"
subsystem: frontend
tags: [javascript, tree-view, collapse, localStorage, owner-groups]
dependency_graph:
  requires: [06-01-PLAN.md]
  provides: [owner-grouped-tree-view, collapse-expand-behavior, localStorage-persistence]
  affects:
    - FilamentCatalog.Service/wwwroot/js/spools.js
    - FilamentCatalog.Service/wwwroot/js/app.js
tech_stack:
  added: []
  patterns: [owner-group tree view rendering, localStorage collapse state, ES module export wiring]
key_files:
  created: []
  modified:
    - FilamentCatalog.Service/wwwroot/js/spools.js
    - FilamentCatalog.Service/wwwroot/js/app.js
decisions:
  - "buildOwnerGroup creates .owner-group divs with header (chevron + name + badge) and .owner-group-rows container"
  - "localStorage collapse state uses fc:collapse:{ownerId} key; value '1'=collapsed, absent=expanded"
  - "applyFilters hides owner groups with 0 visible spool rows after filter application"
  - "initExpandCollapseBtn exported and called from app.js init sequence after initChipFilters"
  - "updateExpandCollapseBtn checks any-collapsed to set button label: Expand all or Collapse all"
metrics:
  duration: "~10 min"
  completed: "2026-05-03"
  tasks_completed: 3
  files_modified: 2
---

# Phase 6 Plan 02: Owner-Grouped Tree View (JS) Summary

Owner-grouped collapsible tree view in spools.js with localStorage-persisted collapse state, group-aware filter hiding, and expand-collapse-all button wiring.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | Remove owner column from buildSpoolRow | 04dc10b | spools.js |
| 2 | Implement owner-grouped tree view + collapse helpers + applyFilters group logic | 81c16c4 | spools.js |
| 3 | Wire initExpandCollapseBtn in app.js | 3a94bdf | app.js |

## What Was Built

**Task 1 — buildSpoolRow cleanup:**
- Removed `ownerEl` creation block (`.spool-owner` div)
- Removed `ownerEl` from `row.append()` call
- Owner name is now displayed in the owner group header, not repeated on every row

**Task 2 — spools.js tree view implementation:**
- Added `expandCollapseBtn` DOM ref
- Added `isCollapsed(ownerId)` / `setCollapsed(ownerId, collapsed)` localStorage helpers using `fc:collapse:{id}` keys
- Added `applyCollapseState(groupEl, collapsed)` which sets `rows.hidden`, `aria-expanded`, and chevron character
- Added `updateExpandCollapseBtn()` which reads all group states and sets button label
- Added `buildOwnerGroup(owner, spools)` — creates `.owner-group` div with header (chevron ▼/▶ + name + badge) and `.owner-group-rows` container; wires click and keyboard (Enter/Space) toggle handlers; applies persisted collapse state on creation
- Replaced flat `renderSpools` with tree-grouped version: groups spools by owner, preserves owner array order, skips owners with no spools
- Replaced `applyFilters` with group-aware version: hides non-matching owner groups when owner filter active; hides groups with 0 visible spool rows after all filters applied
- Exported `initExpandCollapseBtn` which wires the expand/collapse all button click handler

**Task 3 — app.js wiring:**
- Added `initExpandCollapseBtn` to spools.js import
- Added `initExpandCollapseBtn()` call in page-load init block, after `initChipFilters()`

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None — all functionality is fully wired. The owner-group tree view renders live data from `/api/spools` and `/api/owners`; collapse state is persisted immediately to localStorage; filter integration is complete.

## Threat Flags

None — no new network endpoints, auth paths, or file access patterns introduced. XSS mitigations T-06-03 and T-06-04 implemented as planned: `owner.name` rendered via `textContent` only; badge spool count uses string concatenation (not innerHTML). T-06-05 and T-06-06 accepted as planned.

## Self-Check: PASSED

- FilamentCatalog.Service/wwwroot/js/spools.js: FOUND
- FilamentCatalog.Service/wwwroot/js/app.js: FOUND
- Commit 04dc10b: FOUND
- Commit 81c16c4: FOUND
- Commit 3a94bdf: FOUND
