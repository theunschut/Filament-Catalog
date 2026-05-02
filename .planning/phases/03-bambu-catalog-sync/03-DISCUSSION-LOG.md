# Phase 3: Bambu Catalog Sync — Discussion Log

**Date:** 2026-05-02
**Mode:** Advisor (research-backed comparison tables)

---

## Areas Discussed

### 1. Product Picker (Add Spool Dialog)

**Options presented:** Native `<select>` | Search-as-you-type combobox | Two-step: material → color

**Research finding:** Success criteria explicitly requires color swatches, ruling out plain `<select>`. Combobox satisfies the requirement in one step but needs ~100 lines of custom overlay JS + focus management inside `<dialog>`. Two-step avoids the custom overlay but adds cascade state to manage in edit/duplicate dialogs.

**User selection:** Two-step: material → color

**Follow-up — Name auto-fill:**
Options: product title only | variant name only | product title + color name | editable after pick
**User selection:** Product title + color name (e.g. "Bambu Lab PLA Basic — Bambu White")

**Follow-up — Edit behavior:**
Options: restore two-step selects | free-text in edit mode
**User selection:** Restore the two-step selects

---

### 2. Before First Sync

**Options presented:** Disable form + banner | Free-text fallback | Inline sync trigger in dialog

**Research finding:** Since sync is a one-time first-launch action, the disabled state is temporary. Free-text fallback creates permanently diverging code paths. Inline sync trigger in dialog is highest complexity due to native `<dialog>` lifecycle constraints.

**User selection:** Disable form + banner (re-enables reactively, no page reload)

---

### 3. Sync Button Placement

**Options presented:** Filter bar | Sticky header near gear icon | Dedicated sync-status strip

**Research finding:** Agent read actual HTML — header has stat cells and gear icon already. "Last synced: X" reads naturally as catalog metadata. Strip wastes vertical space for a rarely-used feature.

**User selection:** Sticky header near gear icon (using .stat-cell pattern)

---

### 4. Color Field After Product Pick

**Options presented:** Auto-fill + lock | Auto-fill + keep editable | Hide field entirely

**Research finding:** ImageSharp dominant-color works well for solid swatches but can misread gradient/multi-color filaments. For a single-user app, keeping it editable is lowest risk with lowest complexity.

**User selection:** Auto-fill + keep editable

---

## Decisions at Claude's Discretion

- Exact BambuProduct entity fields (beyond the obvious Name, Material, ColorName, ColorHex, LastSyncedAt)
- Whether Spool stores a FK to BambuProduct or copies data at creation time
- Sync status polling interval and response body shape

---

## Deferred Ideas

None.
