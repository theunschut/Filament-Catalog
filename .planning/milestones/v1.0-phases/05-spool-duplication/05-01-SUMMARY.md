---
plan: 05-01
phase: 05-spool-duplication
status: complete
completed: 2026-05-02
---

## Summary

Added a Duplicate button to every spool row and an `openDuplicateDialog` function to `spools.js`. Clicking Duplicate opens the Add Spool dialog pre-filled with the source spool's data in add-mode (title "Duplicate Spool", empty `editId`, Delete button hidden). Saving creates a new spool via `POST /api/spools`; the original is unchanged. CSS `.spool-duplicate-btn` class provides the margin gap between Edit and Duplicate buttons.

## What Was Built

- **`openDuplicateDialog(spool)`** in `spools.js` (line 311): calls `resetFormForAdd()`, repopulates owner select, then fills all form fields from the source spool object. Explicitly sets `dialogTitle.textContent = 'Duplicate Spool'`, `dialog.dataset.editId = ''`, and hides delete/confirm buttons.
- **Duplicate button** in `buildSpoolRow` (line 208–215): `button` element with classes `spool-edit-btn spool-duplicate-btn`, click handler calls `openDuplicateDialog(spool)`. Appended after `editBtn` in `row.append(...)`.
- **`.spool-duplicate-btn` CSS** in `app.css` (line 84–85): `margin-left: var(--space-sm)` to gap the two buttons; hover rule for specificity.

## Key Files

### Created
*(none)*

### Modified
- `FilamentCatalog.Service/wwwroot/js/spools.js` — `openDuplicateDialog` function + Duplicate button in `buildSpoolRow`
- `FilamentCatalog.Service/wwwroot/css/app.css` — `.spool-duplicate-btn` and `.spool-duplicate-btn:hover` rules

## Deviations

None — implementation matches the plan exactly.

## Self-Check: PASSED

- [x] `openDuplicateDialog` function exists in spools.js (line 311)
- [x] `dialogTitle.textContent = 'Duplicate Spool'` present (line 328)
- [x] `dialog.dataset.editId = ''` set explicitly in openDuplicateDialog (line 329)
- [x] `deleteBtn.style.display = 'none'` set in openDuplicateDialog (line 330)
- [x] Duplicate button created with classes `spool-edit-btn spool-duplicate-btn` (line 211)
- [x] Click handler calls `openDuplicateDialog(spool)` (line 213)
- [x] `row.append` includes `duplicateBtn` as last argument (line 215)
- [x] `.spool-duplicate-btn` CSS rule in app.css (line 84)
- [x] No innerHTML used — all values set via `.value`, `.textContent`, or `.style.*`
- [x] Committed: f644e6e
