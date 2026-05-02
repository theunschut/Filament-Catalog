---
status: partial
phase: 03-bambu-catalog-sync
source: [03-05-PLAN.md]
started: 2026-05-02T23:46:49Z
updated: 2026-05-02T23:46:49Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Pre-sync state — Add Spool disabled, banner visible
expected: "Add Spool" button has opacity 0.5 and is disabled; blue "Sync the Bambu catalog first" banner is visible below the filter bar; header shows "Last synced: Never"
result: [pending]

### 2. Sync button triggers sync
expected: Clicking "Sync Bambu catalog" disables the button and shows "Syncing…"; header stat shows "Syncing…" in muted color
result: [pending]

### 3. Sync completes — UI updates
expected: Button re-enables with "Sync Bambu catalog" text; header stat shows a formatted timestamp (e.g. "2 May 2026, 14:32")
result: [pending]

### 4. Catalog gate lifts reactively
expected: "Add Spool" button re-enables and the empty-catalog banner hides — without a page reload
result: [pending]

### 5. Add Spool — material select populated
expected: Opening Add Spool dialog shows a populated Material select (PLA, ABS, PETG, etc.)
result: [pending]

### 6. Add Spool — color select populates on material selection
expected: Selecting a material causes the Color select to populate with color variants for that material
result: [pending]

### 7. Add Spool — auto-fill on color selection
expected: Selecting a color auto-fills Name as "Product Title — Color Name" and fills ColorHex + syncs the color swatch
result: [pending]

### 8. Save spool
expected: Saving the spool succeeds and the spool appears in the list with correct material, name, and color
result: [pending]

### 9. Edit spool — catalog selects pre-populated
expected: Opening Edit dialog pre-selects the correct material and then the correct color variant from the catalog
result: [pending]

### 10. Duplicate spool — catalog selects pre-populated
expected: Opening Duplicate dialog pre-selects the correct material and color, same as Edit
result: [pending]

### 11. Sync error state
expected: If sync fails, button re-enables and stat shows "Sync failed" in destructive color (red)
result: [pending]

## Summary

total: 11
passed: 0
issues: 0
pending: 11
skipped: 0
blocked: 0

## Gaps
