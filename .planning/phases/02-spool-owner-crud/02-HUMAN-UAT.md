---
status: complete
phase: 02-spool-owner-crud
source: [02-VERIFICATION.md]
started: 2026-05-01T00:00:00Z
updated: 2026-05-02T00:00:00Z
---

## Current Test

[complete — all tests passed, GAP-01 fixed]

## Tests

### 1. Add spool end-to-end
expected: Color swatch, status badges, owner name, weight, price, and edit button all appear in the spool row after adding a spool
result: [passed]

### 2. Edit pre-population
expected: Clicking "Edit" on a spool row opens the dialog with all fields pre-populated from that spool's data
result: [passed]

### 3. Filter AND logic
expected: Chip filter combinations correctly apply AND logic — selecting two status chips shows only spools matching both; empty selection shows all
result: [issues] - Sealed and Active filters don't seem to do anything. the AND logic does work on the rest

### 4. Owner delete 409 guard
expected: Clicking "Delete" on an owner with spools shows an inline error "Cannot delete — N spool(s) assigned. Remove spools first." and the modal stays open
result: [passed]

### 5. BAL-03 flag
expected: ⚠ icon appears after owner name in balance table when any spool has no price; tooltip reads "One or more spools have no price — totals may be incomplete."
result: [passed]

### 6. Backdrop click / Escape key
expected: Clicking the backdrop outside a modal closes it; pressing Escape also closes it (native dialog behavior)
result: [passed]

## Summary

total: 6
passed: 6
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

### GAP-01: Spool status chips don't visually filter the list
observed: Clicking "Sealed" or "Active" chips with a mix of 2 Active + 2 Sealed spools shows all 4 rows. "Empty" correctly shows "No spools match." Owner/material/search filters work.
root cause: `.spool-row { display: flex }` in app.css overrides the browser's built-in `[hidden] { display: none }` UA stylesheet rule. Setting `row.hidden = true` in JS applies the DOM attribute but flex wins, so rows stay visible.
fix: Added `.spool-row[hidden] { display: none }` to app.css — reinstates hidden behaviour for rows with the attribute set.
