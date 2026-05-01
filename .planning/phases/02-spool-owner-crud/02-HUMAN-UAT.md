---
status: partial
phase: 02-spool-owner-crud
source: [02-VERIFICATION.md]
started: 2026-05-01T00:00:00Z
updated: 2026-05-01T00:00:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Add spool end-to-end
expected: Color swatch, status badges, owner name, weight, price, and edit button all appear in the spool row after adding a spool
result: [pending]

### 2. Edit pre-population
expected: Clicking "Edit" on a spool row opens the dialog with all fields pre-populated from that spool's data
result: [pending]

### 3. Filter AND logic
expected: Chip filter combinations correctly apply AND logic — selecting two status chips shows only spools matching both; empty selection shows all
result: [pending]

### 4. Owner delete 409 guard
expected: Clicking "Delete" on an owner with spools shows an inline error "Cannot delete — N spool(s) assigned. Remove spools first." and the modal stays open
result: [pending]

### 5. BAL-03 flag
expected: ⚠ icon appears after owner name in balance table when any spool has no price; tooltip reads "One or more spools have no price — totals may be incomplete."
result: [pending]

### 6. Backdrop click / Escape key
expected: Clicking the backdrop outside a modal closes it; pressing Escape also closes it (native dialog behavior)
result: [pending]

## Summary

total: 6
passed: 0
issues: 0
pending: 6
skipped: 0
blocked: 0

## Gaps
