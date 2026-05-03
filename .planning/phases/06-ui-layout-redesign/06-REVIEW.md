---
phase: 06-ui-layout-redesign
reviewed: 2026-05-03T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - FilamentCatalog.Service/wwwroot/index.html
  - FilamentCatalog.Service/wwwroot/css/app.css
  - FilamentCatalog.Service/wwwroot/js/spools.js
  - FilamentCatalog.Service/wwwroot/js/app.js
findings:
  critical: 0
  warning: 6
  info: 4
  total: 10
status: issues_found
---

# Phase 06: Code Review Report

**Reviewed:** 2026-05-03T00:00:00Z
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

This phase implements a two-column layout (sidebar + spool column) and an owner-grouped, collapsible tree view for the spool list. The HTML and CSS changes are structurally sound. The JS logic in `spools.js` and `app.js` is mostly correct, but several edge-case bugs and robustness issues were found. No security vulnerabilities were detected; all DOM text is set via `textContent` and API calls use `encodeURIComponent`. The primary concerns are: a `pollSyncStatus` timeout path that always fires (logic bug), missing `aria-label` on the owner group header, a missing `<label>` for the owner name input, a filter-hide race between collapse state and visibility, and a few quality gaps.

---

## Warnings

### WR-01: `pollSyncStatus` timeout branch always executes after successful completion

**File:** `FilamentCatalog.Service/wwwroot/js/app.js:160-166`
**Issue:** After the `while` loop exits via `break` (on `completed` or `error`) the code unconditionally falls through to lines 162-166, which overwrite `statLastSynced.textContent` with `'Sync timed out'` and reset button state. The "timeout" block runs even when the sync succeeded, instantly clobbering the "last synced" timestamp that was just written at line 133.

The `while` condition (`polls++ < MAX_POLLS`) exits the loop in two ways:
1. `break` — intended early exit on completion/error (loops 1–599)
2. Natural loop exhaustion — only on genuine timeout (loop 600)

Both fall through to the same code block because there is no guard (`return`, flag, or `else`).

**Fix:**
```js
// Replace the post-loop block with a guarded timeout-only path:
async function pollSyncStatus() {
    const POLL_INTERVAL_MS = 500;
    const MAX_POLLS = 600;
    let polls = 0;
    let timedOut = true;   // assume timeout until a break clears it

    while (polls++ < MAX_POLLS) {
        try {
            const status = await getSyncStatus();
            // ... progress display unchanged ...

            if (status.status === 'completed') {
                timedOut = false;
                syncCatalogBtn.textContent = 'Sync Bambu catalog';
                syncCatalogBtn.disabled = false;
                statLastSynced.textContent = formatLastSynced(status.lastSyncedAt);
                statLastSynced.style.color = '';
                const { count } = await getCatalogCount();
                setCatalogGate(count);
                break;
            }
            if (status.status === 'error') {
                timedOut = false;
                syncCatalogBtn.textContent = 'Sync Bambu catalog';
                syncCatalogBtn.disabled = false;
                statLastSynced.textContent = 'Sync failed';
                statLastSynced.style.color = 'var(--color-destructive)';
                console.error('Sync failed:', status.errorMessage);
                break;
            }
        } catch (err) {
            timedOut = false;
            console.error('Polling error:', err);
            syncCatalogBtn.textContent = 'Sync Bambu catalog';
            syncCatalogBtn.disabled = false;
            statLastSynced.textContent = 'Sync failed';
            statLastSynced.style.color = 'var(--color-destructive)';
            break;
        }
        await new Promise(r => setTimeout(r, POLL_INTERVAL_MS));
    }

    if (timedOut) {
        syncCatalogBtn.textContent = 'Sync Bambu catalog';
        syncCatalogBtn.disabled = false;
        statLastSynced.textContent = 'Sync timed out';
        statLastSynced.style.color = 'var(--color-destructive)';
    }
}
```

---

### WR-02: `applyFilters` hides collapsed groups that should remain visible

**File:** `FilamentCatalog.Service/wwwroot/js/spools.js:86-98`
**Issue:** `applyFilters` queries `.spool-row:not([hidden])` to decide whether a group has visible rows. But when a group is collapsed, `rowsContainer.hidden = true` hides the entire container — its child `.spool-row` elements are still in the DOM without individual `hidden` attributes. After a filter is applied, the rows inside a collapsed group appear as "visible" (their own `hidden` attribute is absent), so `visibleRows.length` may be > 0 and the group will not be hidden even when every spool within it is filtered out.

Conversely, when a group is collapsed AND its spools are filtered out, re-expanding it will show rows that should be hidden.

**Fix:** When checking for visible rows inside a group, also check whether those rows are logically visible per the filter result, not just whether they carry `hidden`:
```js
// D-08: hide groups where no spool rows are visible after filter
const visibleRows = group.querySelectorAll('.spool-row[data-id]:not([hidden])');
// If the rows container itself is hidden (collapsed), check the data array instead:
const ownerVisibleCount = visible.filter(s => s.ownerId === ownerId).length;
group.hidden = ownerVisibleCount === 0;
```
Or simpler: set `hidden` on individual spool rows inside collapsed groups when applying filters, and strip it when restoring.

---

### WR-03: Owner group count badge is not updated on filter or re-render

**File:** `FilamentCatalog.Service/wwwroot/js/spools.js:289`
**Issue:** `badge.textContent = spools.length + ' spools'` is set once at build time and never updated. After `renderSpools` is called again (e.g., after an add/edit/delete), new `buildOwnerGroup` calls will have the correct count, but the displayed count may briefly or permanently disagree with the visible filtered count. More critically: after a spool is deleted from an owner who had N spools, the badge still shows N because the DOM is rebuilt — this is fine on full re-render. However, if a future optimisation skips full rebuild, this will silently be wrong. As-is the singular "1 spools" grammar is also a user-visible defect.

**Fix:**
```js
// Fix grammar for singular count
badge.textContent = spools.length === 1 ? '1 spool' : `${spools.length} spools`;
```

---

### WR-04: `buildSpoolPayload` reads `materialSelect` from catalog module's select, not the filter-bar select

**File:** `FilamentCatalog.Service/wwwroot/js/spools.js:449`
**Issue:** `materialSelect` at module scope (line 29) is `document.getElementById('spool-catalog-material')` — the catalog picker in the spool dialog. This is correct. However, `buildSpoolPayload` at line 449 calls `materialSelect.value.trim()`, and when the dialog is opened in edit mode, `restoreCatalogSelectsFromSpool` is fire-and-forget (async, not awaited). If the user clicks "Save" before the async color restore finishes, `materialSelect.value` may still be `''` (blank placeholder left by `resetFormForAdd`), causing the payload to send `material: ""` and the server to reject it with a validation error.

This is a race condition: the catalog restore is async but Save is immediately available.

**Fix:** Disable the Save button while catalog selects are loading, or `await` the restore before calling `showModal`:
```js
async function openEditDialog(spool) {
    resetFormForAdd();
    repopulateOwnerSelect(allOwners);
    saveBtn.disabled = true;
    await restoreCatalogSelectsFromSpool(spool);
    populateFormForEdit(spool);
    saveBtn.disabled = false;
    dialog.showModal();
}
```
Same fix applies to `openDuplicateDialog`.

---

### WR-05: `parseInt(ownerFilter)` called twice in `applyFilters` — redundant and fragile

**File:** `FilamentCatalog.Service/wwwroot/js/spools.js:68, 91`
**Issue:** At line 68, `spool.ownerId !== parseInt(ownerFilter)` converts `ownerFilter` string to int. At line 91 inside the owner group loop, `ownerId !== parseInt(ownerFilter)` converts it again. Both calls occur inside the hot filter path that runs on every keypress. If `ownerFilter` is `''`, `parseInt('')` returns `NaN`; `NaN !== NaN` is always `true`, meaning the guard at line 68 (`if (ownerFilter && ...)`) protects the spool filter but the group-hiding guard at line 91 is only protected by the outer `if (ownerFilter && ...)`. This is currently fine but fragile — a future refactor that removes the outer guard would silently mis-hide groups.

**Fix:** Parse once and reuse:
```js
const ownerFilterId = ownerFilter ? parseInt(ownerFilter) : null;
// Line 68:
if (ownerFilterId !== null && spool.ownerId !== ownerFilterId) return false;
// Line 91:
if (ownerFilterId !== null && ownerId !== ownerFilterId) {
```

---

### WR-06: `#balance-sidebar` sticky `top` is hardcoded to `48px` — breaks if header height changes

**File:** `FilamentCatalog.Service/wwwroot/css/app.css:119`
**Issue:** `#balance-sidebar { top: 48px; }` is a magic number tied to the `#summary-bar { height: 48px }` rule. If the summary bar height ever changes (e.g., wraps on narrow viewports, or filter-bar is considered part of the sticky context), the sidebar will overlap or underlap the header. The filter bar (`#filter-bar`) is not sticky, so the sidebar can scroll behind it.

**Fix:** Define a CSS custom property for the header height and reuse it:
```css
:root {
    --header-height: 48px;
}
#summary-bar { height: var(--header-height); }
#balance-sidebar { top: var(--header-height); }
```

---

## Info

### IN-01: Missing `aria-label` on owner group header — role="button" without accessible name

**File:** `FilamentCatalog.Service/wwwroot/js/spools.js:275-276`
**Issue:** `header.setAttribute('role', 'button')` is set but no `aria-label` is provided. Screen readers will announce the concatenated text content of the header ("▼ Alice 3 spools"), which is usable but non-ideal. The chevron character "▼"/"▶" will be read aloud as "down-pointing triangle" or similar. The `aria-expanded` attribute is present (good), but an explicit label would be cleaner.

**Fix:**
```js
header.setAttribute('aria-label', `Toggle ${owner.name} group`);
```
Or wrap the chevron in `<span aria-hidden="true">` to exclude it from the accessible name computation.

---

### IN-02: `<label>` missing for owner name input in owner dialog

**File:** `FilamentCatalog.Service/wwwroot/index.html:168`
**Issue:** The `#owner-name-input` field has a `placeholder` but no associated `<label>`. Placeholders disappear on focus and are not a substitute for a label. This is an accessibility gap.

**Fix:**
```html
<div class="owner-add-form">
  <label for="owner-name-input" class="sr-only">Owner name</label>
  <input type="text" id="owner-name-input" placeholder="Owner name" />
  <button type="button" id="owner-add-btn" class="btn-primary">Add Owner</button>
</div>
```
Add a `.sr-only` utility class (visually hidden but screen-reader accessible) if one is not already defined.

---

### IN-03: `console.error` calls left in production code paths

**File:** `FilamentCatalog.Service/wwwroot/js/app.js:48, 62`; `spools.js` (via catalog.js:45)
**Issue:** `console.error('Failed to refresh after spool mutation:', err)` and similar are the only error feedback in the `onSpoolDialogClose` and `owners-updated` callbacks. If a refresh fails after mutation, the user sees no indication that the displayed data may be stale. These are silently swallowed with only a console message.

**Fix:** Surface refresh failures with a visible toast or banner rather than silent `console.error`. At minimum, add a visible DOM indicator that data may be stale.

---

### IN-04: `.owner-spool-count` badge has no `padding` rule — inherits from `.badge` but visual sizing relies on implicit spacing

**File:** `FilamentCatalog.Service/wwwroot/css/app.css:162-165`
**Issue:** `.owner-spool-count` sets `background`, `color`, and `border` but not `padding`. It relies on inheriting from `.badge` (which provides `padding: 2px 8px`). The rule appears after `.badge` in the cascade, so this works today. However, if `.badge` is ever refactored to not include padding, or a more specific rule overrides it, the count badge will lose its padding silently. The intent should be explicit.

**Fix:** Either document the intentional inheritance with a comment, or add an explicit `padding` to `.owner-spool-count`.

---

_Reviewed: 2026-05-03T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
