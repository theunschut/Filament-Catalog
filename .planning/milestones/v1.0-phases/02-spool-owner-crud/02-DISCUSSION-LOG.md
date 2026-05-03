# Phase 2: Spool & Owner CRUD - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-01
**Phase:** 02-spool-owner-crud
**Areas discussed:** Spool↔product link, Page layout, Filter UX, Owner management placement

---

## Spool ↔ product link

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid: own columns + optional FK | Spool stores Name/Material/ColorHex directly (non-null). BambuProductId nullable FK added in Phase 3's migration. Phase 2 form uses free text, zero null-guard debt. | ✓ |
| Denormalized only (no FK ever) | Spool stores product fields, no FK to BambuProduct ever. Phase 3 just pre-fills form fields. Simpler schema, harder to retrofit catalog-linked history later. | |
| Nullable FK only | Spool.BambuProductId? only. Phase 2 form shows empty dropdown — broken UX until Phase 3. | |

**User's choice:** Hybrid: own columns + optional FK (Recommended)
**Notes:** Research confirmed this is the standard pattern for this sequencing scenario — self-describing Spool avoids null-guard debt in Phase 2 while providing a clean Phase 3 hook-in.

---

## Page layout

| Option | Description | Selected |
|--------|-------------|----------|
| Sticky summary bar + collapsible balance | Summary totals pinned at top. Spool list as main body. Balance + owners in collapsible section below. | ✓ |
| Two-column layout | Spool list left, summary/balance/owners always visible on the right. Good for simultaneous cross-reference. | |
| Single scrolling page | All sections stacked vertically, no JS layout. Summary and balance scroll out of view. | |

**User's choice:** Sticky summary bar + collapsible balance (Recommended)
**Notes:** Matches the typical usage pattern — add/review spools as primary task, check balance occasionally.

---

## Filter UX

| Option | Description | Selected |
|--------|-------------|----------|
| Horizontal bar + live filtering + chips for enums | Filter bar above list. Owner/material as `<select>`. Spool/payment status as chip buttons. Free text input. Instant filtering. | ✓ |
| Horizontal bar + Apply button + all dropdowns | All 5 filters as `<select>`, fires only on Apply click. Slower for single-filter use. | |

**User's choice:** Horizontal bar + live filtering + chips for enums (Recommended)
**Notes:** Chips for 3-value enums (sealed/active/empty, paid/unpaid/partial) surface all options at a glance. No debounce concern at single-user data scale.

---

## Owner management placement

| Option | Description | Selected |
|--------|-------------|----------|
| Settings modal via gear icon | Gear icon in header opens native `<dialog>`. On close, dispatches event to refresh owner dropdowns. | ✓ |
| Inside the collapsible balance section | Owner CRUD alongside balance table. No modal needed but cluttered. | |
| Dedicated /settings page | Second HTML page with nav. Adds routing concept to a single-page tool. | |

**User's choice:** Settings modal via gear icon (Recommended)
**Notes:** Native `<dialog>` is already in the stack per CLAUDE.md. Keeps main page clean.

---

## Claude's Discretion

- Exact HTML/CSS styling, color scheme, and visual design
- HTTP status code choices within the noted ranges
- Whether to use CSS custom properties for colors
- Chip button styling (border, active color treatment)

## Deferred Ideas

None — discussion stayed within phase scope.
