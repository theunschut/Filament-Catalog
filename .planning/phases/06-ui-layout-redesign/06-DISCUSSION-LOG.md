# Phase 6: UI Layout Redesign - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-03
**Phase:** 6-UI Layout Redesign
**Areas discussed:** Filter bar placement, Sidebar content scope, Owner row visual, Collapse default & persistence

---

## Filter Bar Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Full width, above both columns | Filter bar spans entire page width — same as today | |
| Above spool column only | Filter bar constrained to the spool list column width | ✓ |
| Inside spool column, no change | Filter bar stays as-is, just scoped by layout context | |

**User's choice:** Above spool column only

---

| Option | Description | Selected |
|--------|-------------|----------|
| Sidebar starts alongside the filter bar | Two-column layout begins right after sticky header | ✓ |
| Sidebar starts only alongside the spool list | Filter bar is a single-column strip; two columns start below | |

**User's choice:** Sidebar starts alongside the filter bar

---

| Option | Description | Selected |
|--------|-------------|----------|
| Sticky sidebar, spool list scrolls independently | Sidebar stays fixed; CSS position: sticky | ✓ |
| Both columns scroll together | Whole page scrolls; balance disappears when scrolling | |

**User's choice:** Sticky sidebar

---

## Sidebar Content Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Move stats into the sidebar | Sidebar contains summary stats + balance table; header becomes minimal | |
| Stats stay in the header | Sidebar contains only the balance table | ✓ |

**User's choice:** Stats stay in the header

---

| Option | Description | Selected |
|--------|-------------|----------|
| 240px fixed | Comfortable for balance table columns | ✓ |
| 280px fixed | Roomier for long owner names | |
| You decide | Planner picks width | |

**User's choice:** 240px fixed

---

| Option | Description | Selected |
|--------|-------------|----------|
| Always visible, no toggle | Remove the `<details>` collapsible wrapper | ✓ |
| Keep the collapsible toggle | User can still collapse inside the sidebar | |

**User's choice:** Always visible, no toggle

---

## Owner Row Visual

| Option | Description | Selected |
|--------|-------------|----------|
| Section header row with bg color | Full-width row with --color-bg background, chevron, spool count badge | ✓ |
| Bold row, same background as spools | Same background as spool rows, just bold + triangle | |

**User's choice:** Section header row with background color

---

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it — collapses other owners when one is selected | Owner filter hides other owner groups | ✓ |
| Keep it — highlights but shows all | Filter dims non-matching rows but keeps all headers | |
| Remove the owner filter dropdown | Groups make the dropdown redundant | |

**User's choice:** Keep — collapses other owner groups when one is selected

---

| Option | Description | Selected |
|--------|-------------|----------|
| Hide empty owner groups | Owner header hidden if all its spools are filtered out | ✓ |
| Show empty owner groups | Header stays with "0 spools" count | |

**User's choice:** Hide empty owner groups

---

## Collapse Default & Persistence

| Option | Description | Selected |
|--------|-------------|----------|
| All owner groups expanded | Every group visible on first load | ✓ |
| All owner groups collapsed | Only headers visible on load | |

**User's choice:** All expanded on load

---

| Option | Description | Selected |
|--------|-------------|----------|
| No persistence — always reset to expanded | Simpler, no stale state | |
| Persist per owner in localStorage | User preferences survive reloads | ✓ |

**User's choice:** Persist in localStorage

---

| Option | Description | Selected |
|--------|-------------|----------|
| Ignore stale entries — they're harmless | Missing keys default to expanded | ✓ |
| Clean up on owner list load | Remove stale keys for deleted owners | |

**User's choice:** Ignore stale entries

---

| Option | Description | Selected |
|--------|-------------|----------|
| fc-owner-{id}-collapsed | Prefixed with 'fc-' namespace | |
| owner-{id}-collapsed | Simpler, no prefix | |
| You decide | Planner picks key scheme | ✓ |

**User's choice:** You decide (planner discretion)

---

**Free-text addition:** User requested an expand/collapse all button for the tree view.

| Option | Description | Selected |
|--------|-------------|----------|
| In the filter bar, right side | Secondary button next to Add Spool | ✓ |
| Above the spool list, inline header | Toggle inside the spool list area | |
| You decide | Planner picks placement | |

**User's choice:** In the filter bar, right side

---

| Option | Description | Selected |
|--------|-------------|----------|
| Single toggle button | "Collapse all" / "Expand all" based on current state | ✓ |
| Two separate buttons | Expand all + Collapse all always visible | |

**User's choice:** Single toggle button

---

## Claude's Discretion

- localStorage key scheme for collapse state (namespaced prefix recommended)
- Exact chevron character or CSS triangle for disclosure indicator
- Semantic element for sidebar (`<aside>` vs `<section>`) and its border/shadow treatment

## Deferred Ideas

None — discussion stayed within phase scope.
