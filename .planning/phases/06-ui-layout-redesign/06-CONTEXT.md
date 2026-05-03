# Phase 6: UI Layout Redesign - Context

**Gathered:** 2026-05-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Restructure the page layout into a two-column design: a fixed-width balance sidebar on the left, and a spool column (filter bar + owner-grouped collapsible tree view) on the right. No new data, no new API endpoints — pure frontend restructuring.

</domain>

<decisions>
## Implementation Decisions

### Page Layout Structure
- **D-01:** Two-column layout begins immediately after the sticky header (`#summary-bar`). Left column = balance sidebar (240px fixed width); right column = filter bar stacked on top of spool list.
- **D-02:** Filter bar is constrained to the spool column only — it does NOT span the full page width. The sidebar is visible alongside the filter bar at the same vertical level.
- **D-03:** Sidebar is sticky — stays fixed in place while the spool list column scrolls independently. CSS approach: `position: sticky` on the sidebar, spool column is a scrolling container.

### Sidebar Content
- **D-04:** Sidebar contains ONLY the balance table. Summary stats (Total Spools, My Spools, Total Value, Total Owed) stay in the sticky header — no change to the header layout.
- **D-05:** Sidebar is always visible — the `<details>` collapsible wrapper is removed. Balance table renders directly without a toggle. Width: 240px fixed.

### Owner-Grouped Tree View
- **D-06:** Owner nodes are section header rows styled with `background: var(--color-bg)` (the page background color, distinct from spool row white). Each owner row shows: chevron (▶ collapsed / ▼ expanded), owner name, and a spool count badge.
- **D-07:** The "filter by owner" dropdown in the filter bar is kept. When an owner is selected, all other owner groups are hidden — only the selected owner's group is shown. Selecting "All owners" restores all groups.
- **D-08:** Owner groups with zero visible spools (after filter application) are hidden entirely — no empty group headers shown.

### Expand/Collapse All
- **D-09:** A single toggle button in the filter bar (right side, alongside the Add Spool button) controls all groups at once. Label reads "Collapse all" when all groups are expanded; "Expand all" when any group is collapsed.

### Collapse State Persistence
- **D-10:** Default state on page load = all owner groups expanded.
- **D-11:** Each owner group's collapsed/expanded state persists in localStorage, keyed by owner ID. Key scheme: planner decides (prefix with app namespace to avoid collisions).
- **D-12:** Stale localStorage entries for deleted owners are ignored — missing key defaults to expanded. No cleanup needed.

### Claude's Discretion
- localStorage key scheme for collapse state (planner decides — use a namespaced prefix)
- Exact chevron character or CSS triangle approach for the disclosure indicator
- Whether the sidebar has a `<nav>` or `<aside>` semantic element, and its border/shadow treatment

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Design System & Conventions
- `CLAUDE.md` — critical conventions: ES modules, `textContent` only (no innerHTML with user data), native `<dialog>`, `DateTime.UtcNow`
- `FilamentCatalog.Service/wwwroot/css/app.css` — all design tokens (--color-bg, --color-surface, --color-border, --color-muted, --color-accent, --space-*), existing component styles, layout rules for #summary-bar and #filter-bar

### HTML Structure (integration points)
- `FilamentCatalog.Service/wwwroot/index.html` — current page structure: #summary-bar (sticky header), #filter-bar, `<main id="spool-list">`, `<details id="balance-section">`. This is what gets restructured.

### JavaScript Modules (integration points)
- `FilamentCatalog.Service/wwwroot/js/spools.js` — renders spool rows and owns filter logic; tree view rendering and collapse toggle go here
- `FilamentCatalog.Service/wwwroot/js/summary.js` — populates the balance table; integration point for sidebar rendering
- `FilamentCatalog.Service/wwwroot/js/app.js` — module init and wiring

### Roadmap
- `.planning/ROADMAP.md` §Phase 6 — success criteria (4 items): sidebar fixed width, owner-grouped collapsible tree, collapse/expand behavior, spool actions accessible within tree

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `.stat-cell` pattern (app.css + index.html): summary stats cells — unchanged, stay in header
- `.spool-row` (app.css + spools.js): existing spool row style; tree view spool rows reuse this, indented under owner headers
- `#balance-section` `<details>` (index.html + app.css): current balance wrapper — the `<details>` element is replaced by a plain `<aside>` or `<section>` in the sidebar; the `#balance-table` inside it stays unchanged
- `.badge` pattern (app.css): existing badge styles — owner spool count badge reuses `.badge` styling

### Established Patterns
- ES modules with `type="module"` — new JS behavior (collapse toggle, localStorage) goes in spools.js
- `textContent` only for user-supplied data — owner names in group headers must use `textContent`, not innerHTML
- No bundler, no framework — plain DOM manipulation
- `#spool-list` is a `<main>` element — tree view renders into this same element; planner should check whether `<main>` semantics still fit with nested owner groups

### Integration Points
- `applyFilters()` in spools.js currently shows/hides `.spool-row[data-id]` elements; with tree view, it must also show/hide owner group headers (D-07, D-08)
- `renderSpools()` (or equivalent) in spools.js builds the DOM; needs to change from flat rows to grouped structure
- `summary.js` populates `#balance-table tbody` — this table moves to the sidebar but the JS logic is unchanged
- Owner filter dropdown (`#filter-owner`) behavior changes: selecting an owner now hides other groups, not just individual rows

</code_context>

<specifics>
## Specific Ideas

- Owner group header row structure: `[chevron] [owner name] [spool count badge]` — full width, background `var(--color-bg)`
- Expand/Collapse All button: single `<button class="btn-secondary">` in the filter bar, right side (near Add Spool). Label toggles between "Collapse all" and "Expand all" based on current state of all groups.
- Sidebar structure: `<aside id="balance-sidebar">` with a heading ("Balance Overview" or similar), then `<table id="balance-table">` directly — no `<details>` wrapper.
- Two-column wrapper: a new `<div id="main-layout">` (or similar) wraps the sidebar and the spool column after the sticky header.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 6-UI Layout Redesign*
*Context gathered: 2026-05-03*
