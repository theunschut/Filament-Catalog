---
phase: 06-ui-layout-redesign
plan: "01"
subsystem: frontend
tags: [html, css, layout, sidebar, tree-view]
dependency_graph:
  requires: []
  provides: [two-column-layout, balance-sidebar, spool-column, expand-collapse-btn, owner-group-css]
  affects: [FilamentCatalog.Service/wwwroot/index.html, FilamentCatalog.Service/wwwroot/css/app.css]
tech_stack:
  added: []
  patterns: [two-column flex layout, sticky sidebar, owner group tree view CSS]
key_files:
  created: []
  modified:
    - FilamentCatalog.Service/wwwroot/index.html
    - FilamentCatalog.Service/wwwroot/css/app.css
decisions:
  - "Two-column layout using flex on #main-layout; sidebar 240px fixed width, spool column flex: 1"
  - "balance-sidebar aside with sticky top: 48px (matches header height); no details wrapper"
  - "#expand-collapse-btn placed immediately before #add-spool-btn in filter bar"
  - "Removed #balance-section and #balance-section summary CSS rules; kept all #balance-table rules intact"
metrics:
  duration: "~5 min"
  completed: "2026-05-03"
  tasks_completed: 2
  files_modified: 2
---

# Phase 6 Plan 01: HTML+CSS Layout Restructure Summary

Two-column layout with sticky balance sidebar and tree-view CSS component rules, replacing the old collapsible details balance section.

## Tasks Completed

| # | Task | Commit | Files |
|---|------|--------|-------|
| 1 | Restructure index.html into two-column layout | 73186a2 | index.html |
| 2 | Add layout and tree-view CSS rules; remove obsolete balance-section rules | e00ed5d | app.css |

## What Was Built

**Task 1 — index.html restructure:**
- Replaced the three flat elements (`#filter-bar`, `#spool-list`, `<details id="balance-section">`) with a `#main-layout` two-column flex wrapper
- Left column: `<aside id="balance-sidebar">` containing `<h2>Balance Overview</h2>` and `#balance-table` directly (no details wrapper)
- Right column: `<div id="spool-column">` containing `#filter-bar` (with new `#expand-collapse-btn`) and `#spool-list`
- All JS-referenced IDs preserved: `filter-owner`, `filter-material`, `filter-search`, `add-spool-btn`, `balance-table`, `spool-list`, `catalog-empty-notice`

**Task 2 — app.css changes:**
- Removed: `#balance-section` and `#balance-section summary` rules (3 declarations)
- Added Phase 6 section with: `#main-layout` (flex wrapper), `#balance-sidebar` (240px sticky), `#balance-sidebar h2` (heading style), `#spool-column` (flex: 1), and owner group tree-view classes (`.owner-group-header`, `.owner-chevron`, `.owner-group-name`, `.owner-spool-count`, `.owner-group-rows .spool-row`)

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None — this plan delivers structural HTML/CSS only. The owner-group tree-view CSS classes are wired up but the corresponding JS rendering (grouping spools by owner) is intentionally deferred to Plan 02 (06-02-PLAN.md). The page will render with the sidebar and filter bar in place but the spool list will remain flat until Plan 02 completes.

## Threat Flags

None — static HTML/CSS changes only; no new network endpoints, auth paths, or trust boundaries introduced.

## Self-Check: PASSED

- FilamentCatalog.Service/wwwroot/index.html: FOUND
- FilamentCatalog.Service/wwwroot/css/app.css: FOUND
- Commit 73186a2: FOUND
- Commit e00ed5d: FOUND
