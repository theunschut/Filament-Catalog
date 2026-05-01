---
phase: 02-spool-owner-crud
reviewed: 2026-05-01T09:00:00Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - FilamentCatalog/Models/Spool.cs
  - FilamentCatalog/Models/PaymentStatus.cs
  - FilamentCatalog/Models/SpoolStatus.cs
  - FilamentCatalog/AppDbContext.cs
  - FilamentCatalog/Migrations/20260501070939_AddSpools.cs
  - FilamentCatalog/Program.cs
  - FilamentCatalog/wwwroot/index.html
  - FilamentCatalog/wwwroot/css/app.css
  - FilamentCatalog/wwwroot/js/api.js
  - FilamentCatalog/wwwroot/js/summary.js
  - FilamentCatalog/wwwroot/js/owners.js
  - FilamentCatalog/wwwroot/js/spools.js
  - FilamentCatalog/wwwroot/js/app.js
findings:
  critical: 3
  warning: 6
  info: 3
  total: 12
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-05-01T09:00:00Z
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

This phase delivers Spool and Owner CRUD on top of the existing EF Core / SQLite stack. The server-side code is generally sound: migrations, model definitions, and API endpoints are correct. The dominant failure cluster is in the JavaScript frontend.

Three blockers were found:

1. The `spool.colorHex` value is assigned directly into `element.style.background` without any sanitisation — a stored CSS-injection vector.
2. The `ownerId === 0` owner-required guard silently passes when `parseInt` returns `NaN` (no owner in the select).
3. The `onSpoolDialogClose` callback fires on every `close` event, including the cancel/discard path, causing a full network refresh on cancellation — not a crash, but the same callback also runs after the owner modal is closed indirectly, producing double refreshes; more critically, the `owners-updated` event fires unconditionally when the owner dialog closes (even if nothing changed), which is a latent correctness issue as the codebase grows.

Six warnings cover: missing `ColorHex` format validation on the server, unguarded `parseInt(ownerSelect.value)` that can produce `NaN` sent to the server, filter state surviving `renderSpools` re-renders (chips stay active but the Sets are never cleared after a full reload), the empty-state logic divergence between zero-spools and filtered-zero, `openModal` not handling the async error path, and the `owners-updated` event firing even when the dialog is dismissed without changes.

---

## Critical Issues

### CR-01: Unsanitised `colorHex` assigned to `style.background` — CSS injection

**File:** `FilamentCatalog/wwwroot/js/spools.js:161`
**Issue:** `spool.colorHex` is a server-persisted string written verbatim into `element.style.background`. Although this is a local-only app, any `colorHex` value containing a CSS expression (e.g. `url(javascript:...)` in legacy engines, or `expression(...)` in IE) will execute in the browser. More concretely: any string that is not a valid hex colour will silently render an unexpected style. The server stores whatever string the client sends — no format check exists on the server side — so a bad actor (or a bug in `buildSpoolPayload`) can store an arbitrary string.

The same assignment occurs in `buildSpoolRow` (line 161) and in `populateFormForEdit` (line 278, via `colorSwatch.style.background = colorPicker.value` which is safe, but `colorHexInput.value` is set to the raw `spool.colorHex` on line 277 without validation first, and then `colorSwatch.style.background = colorPicker.value` relies on the browser clamping the color picker, leaving the hex input showing an untrusted value).

**Fix:** Validate and sanitise before any DOM assignment. A one-liner guard is sufficient:

```js
// spools.js — helper used everywhere colorHex touches the DOM
function safeColorHex(hex) {
    return /^#[0-9A-Fa-f]{6}$/.test(hex) ? hex : '#888888';
}

// line 161 — buildSpoolRow
swatch.style.background = safeColorHex(spool.colorHex);

// line 275-278 — populateFormForEdit
const hex = safeColorHex(spool.colorHex ?? '#888888');
colorPicker.value    = hex;
colorHexInput.value  = hex;
colorSwatch.style.background = hex;
```

Also add a server-side validation in `Program.cs` in both POST and PUT spool handlers:

```csharp
var hexRe = new System.Text.RegularExpressions.Regex(@"^#[0-9A-Fa-f]{6}$");
if (!string.IsNullOrWhiteSpace(req.ColorHex) && !hexRe.IsMatch(req.ColorHex))
    return Results.UnprocessableEntity(new { error = "ColorHex must be in #RRGGBB format." });
```

---

### CR-02: `ownerId` guard uses falsy check — passes silently when `parseInt` returns `NaN`

**File:** `FilamentCatalog/wwwroot/js/spools.js:328`
**Issue:** The save handler checks `if (!payload.ownerId)` to detect a missing owner. `buildSpoolPayload` computes `ownerId: parseInt(ownerSelect.value)`. If `ownerSelect` has no options (e.g. `getOwners()` returned an empty array, or the select was never populated), `ownerSelect.value` is `""` and `parseInt("")` returns `NaN`. The check `!NaN` evaluates to `true` — so the guard does fire in that case. However, if `ownerSelect.value` is `"0"` (a hypothetical owner with id 0, or a stale select option with value `"0"`), `parseInt("0")` returns `0` and `!0` is `true` — the guard fires spuriously. More importantly, if `ownerSelect` is somehow populated with a non-numeric value such as an empty string that slips through, `NaN` is sent in the JSON body, which the .NET JSON deserializer will reject with a 400, surfacing a confusing generic error rather than "Owner is required." The guard should use an explicit `isNaN` check.

**Fix:**
```js
// spools.js line 328
if (isNaN(payload.ownerId) || payload.ownerId <= 0) {
    showDialogError('Owner is required.');
    return;
}
```

And in `buildSpoolPayload` (line 311), ensure the fallback is explicit:
```js
ownerId: parseInt(ownerSelect.value, 10) || null,
```
Then the `isNaN` check on `null` will catch the empty-select case correctly.

---

### CR-03: Filter chip state (Sets) is never reset on full data reload — stale filter produces incorrect results after refresh

**File:** `FilamentCatalog/wwwroot/js/spools.js:10-11` and `212`
**Issue:** `spoolStatusFilter` and `paymentStatusFilter` are module-level `Set` instances. They are populated by chip clicks in `initChipFilters` and never cleared. When `renderSpools` is called after a spool add/edit/delete (via `onSpoolDialogClose`), the Sets retain their previous state, so `applyFilters()` on line 242 correctly re-applies them. That part is intentional.

The bug is more subtle: when the page first loads, both Sets are empty (show-all). A user activates a chip (e.g. "Sealed"). The spool dialog is opened, the spool is edited to status "Active", and saved. `renderSpools` is called. The chip still shows as visually active (the `.active` CSS class on the button was set by `initChipFilters` click handler and is never touched by `renderSpools`), and the Set still contains "Sealed", so the just-edited spool (now "Active") disappears from view — but the chip UI correctly reflects the active filter so that is actually correct behaviour.

The real bug: `initChipFilters` is called once at page load. If `renderSpools` is called multiple times (it is — both at init and after every mutation), the chip buttons' `active` CSS class state and the Set contents stay consistent. **However**, `repopulateOwnerFilter` and `repopulateOwnerSelect` are called inside `renderSpools` (lines 240-241), resetting the owner/material dropdowns, but the chip Sets are not synchronised with the chip DOM state. If an external caller were to clear `.active` classes on chips without updating the Sets, or vice versa, the filter would silently malfunction. More practically: after a full page re-render, `applyFilters` is called from within `renderSpools`, but `filterOwner.value` and `filterMat.value` may have been reset by `repopulateOwnerFilter`/`repopulateMaterialSelect` just before — both functions try to preserve the current selection, but if the previously selected owner was deleted, the value falls back to `''`, which is correct. The chip Sets, however, are never consulted or validated against the current data. This is an architectural inconsistency that will cause silent mismatch as the codebase grows.

The immediate, provable correctness bug: `repopulateOwnerFilter` is called from both `renderSpools` (line 240) and from the `owners-updated` event handler in `app.js` (line 47). When an owner is deleted and `owners-updated` fires, `renderSpools` is NOT called, so `allSpools` may still reference spools assigned to the deleted owner. `applyFilters` then tries to show/hide rows for spools that reference a now-gone owner — the rows exist in the DOM and in `allSpools`, so they are shown, but the owner dropdown no longer lists that owner. A spool can appear with a dangling owner reference in the UI. (The server correctly prevents owner deletion when spools are assigned, so this only manifests if the server check is bypassed — but the client-side state is still inconsistent.)

**Fix:** After `repopulateOwnerFilter`, call `applyFilters()` again — which `renderSpools` already does. The `owners-updated` handler in `app.js` should also refresh the spool list:

```js
// app.js — owners-updated handler (line 44)
document.addEventListener('owners-updated', async () => {
    try {
        const [spools, owners, summary, balance] = await Promise.all([
            getSpools(), getOwners(), getSummary(), getBalance()
        ]);
        renderSpools(spools, owners);   // re-render spools with updated owner list
        renderSummary(summary);
        renderBalance(balance);
    } catch (err) {
        console.error('Failed to refresh after owner mutation:', err);
    }
});
```

---

## Warnings

### WR-01: `ColorHex` not validated on the server — arbitrary strings persist to the database

**File:** `FilamentCatalog/Program.cs:88` and `120`
**Issue:** Both the POST and PUT spool handlers fall back to `"#888888"` when `ColorHex` is null/whitespace, but accept any non-empty string as-is. A client can store `"; color: red; --x: url(x)"` or any other string in the `ColorHex` column. There is no server-side regex check. Because `colorHex` is later rendered into `style.background` (see CR-01), this is a persistence vector for the CSS injection described there.

**Fix:** Add regex validation in both handlers (see CR-01 fix above).

---

### WR-02: `onSpoolDialogClose` fires on every dialog close — including cancel

**File:** `FilamentCatalog/wwwroot/js/spools.js:405-407` and `app.js:30-41`
**Issue:** `onSpoolDialogClose` registers a `close` event listener on the `<dialog>` element. The `close` event fires when the dialog is dismissed for any reason: save, delete, cancel button, backdrop click, or Escape key. This means `app.js` issues four parallel API requests every time the user cancels the dialog, discards changes, or presses Escape — even though no data changed. On a localhost service this is a minor UX issue, but it also means the filter state (owner dropdown position, search text) may be disturbed by the re-render on cancel.

**Fix:** Use a boolean flag or a custom event to distinguish confirmed mutations from cancellations:

```js
// spools.js — after a successful save or delete, set a flag before closing
let _mutationOccurred = false;

// in the save handler, after await updateSpool / createSpool:
_mutationOccurred = true;
dialog.close();

// in the delete handler:
_mutationOccurred = true;
dialog.close();

// in onSpoolDialogClose:
export function onSpoolDialogClose(callback) {
    dialog.addEventListener('close', () => {
        if (_mutationOccurred) {
            _mutationOccurred = false;
            callback();
        }
    });
}
```

---

### WR-03: `owners-updated` event fires unconditionally on every owner dialog close

**File:** `FilamentCatalog/wwwroot/js/owners.js:75-77`
**Issue:** The `close` event listener on the owner dialog always dispatches `owners-updated`, even when the dialog was opened and closed without any changes (e.g. user opened it to check owners, then pressed Escape or clicked the backdrop). The `app.js` handler responds by fetching owners, updating filter selects, and calling `refreshSummaryAndBalance` — three API calls every time the gear icon dialog is dismissed without action.

**Fix:** Use the same mutation-flag pattern described in WR-02. Set a flag when `createOwner` or `deleteOwner` succeeds, and only dispatch `owners-updated` when the flag is set.

---

### WR-04: `openModal` in `owners.js` swallows async errors silently

**File:** `FilamentCatalog/wwwroot/js/owners.js:63-69`
**Issue:** `openModal` calls `await getOwners()` without a try/catch. If the fetch fails (network error, server down), the promise rejects and the unhandled rejection propagates to the browser console — no error is shown to the user. The dialog is never opened, and the user sees nothing.

**Fix:**
```js
async function openModal() {
    clearError();
    nameInput.value = '';
    try {
        const owners = await getOwners();
        renderOwnerList(owners);
        dialog.showModal();
    } catch (err) {
        showError('Could not load owners. Is the service running?');
        dialog.showModal();  // open dialog so the error is visible
    }
}
```

---

### WR-05: `applyFilters` empty-state logic is inverted for the filtered-zero case

**File:** `FilamentCatalog/wwwroot/js/spools.js:86-87`
**Issue:** The condition that hides/shows the empty-state element is:
```js
emptyEl.hidden = visible.length > 0 || allSpools.length === 0;
```
When `allSpools.length > 0` and `visible.length === 0` (filters exclude everything): `hidden = false` — correct, the empty state is shown.
When `allSpools.length === 0` and `visible.length === 0`: `hidden = true` — **incorrect**. The empty state is hidden when there are no spools at all. But `renderSpools` handles this case separately by building a non-hidden empty-state element when `spools.length === 0` (line 218-226). So in practice, `applyFilters` is only called after `renderSpools` when `allSpools.length > 0`, making this dead-but-wrong code.

The subtle bug: if `applyFilters` is ever called when `allSpools` is empty (e.g. after all spools are deleted, which triggers a re-render through the `close` callback), the empty-state element built by the `spools.length === 0` branch has `hidden` unset (defaults to `false`). Then `applyFilters` runs and sets `hidden = true` — hiding the "No spools yet" message.

**Fix:**
```js
// applyFilters — line 87
emptyEl.hidden = visible.length > 0;
// Remove the || allSpools.length === 0 condition — that case is handled by renderSpools
```
Or guard the entire empty-state block with `if (allSpools.length > 0)`.

---

### WR-06: `PricePaid` stored as `decimal` / SQLite `TEXT` — floating-point round-trip via `parseFloat` in JS

**File:** `FilamentCatalog/Program.cs:210` and `FilamentCatalog/wwwroot/js/spools.js:312`
**Issue:** The migration stores `PricePaid` as `TEXT` in SQLite (EF Core maps `decimal` to `TEXT` for SQLite). The JS client builds the payload with `parseFloat(priceInput.value)` which produces a JavaScript IEEE-754 double. When serialised to JSON and deserialised by the .NET JSON deserialiser into `decimal?`, there is potential for precision loss on values like `24.99` (which cannot be represented exactly as a double). The decimal serialisation then stores a TEXT representation of the double-rounded value.

For a local filament tracker this is unlikely to cause real-world errors, but it is an architectural mismatch that could produce display inconsistencies (e.g. "24.990000000000001" in the stored TEXT).

**Fix:** Use `step="0.01"` on the price input (already present — good) and send the value as a string in the JSON payload, letting .NET parse it as `decimal` directly:
```js
pricePaid: priceInput.value ? priceInput.value : null,
// .NET will deserialise the string "24.99" to decimal 24.99 exactly
```
Alternatively, round to 2 decimal places before sending: `Math.round(parseFloat(priceInput.value) * 100) / 100`.

---

## Info

### IN-01: `Spool.CreatedAt` has no default — relies on caller always setting `DateTime.UtcNow`

**File:** `FilamentCatalog/Models/Spool.cs:14`
**Issue:** `CreatedAt` is a non-nullable `DateTime` with no default value initialiser or EF Core value generator. Two places set it correctly (`Program.cs:100` POST, not at all in PUT — which is fine for PUT). However, any future code path that constructs a `Spool` without explicitly setting `CreatedAt` will silently store `0001-01-01 00:00:00` (the `default(DateTime)` value), and EF Core will not warn. A value generator would make this invariant self-enforcing.

**Fix:**
```csharp
// Spool.cs
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```
Or configure it in `AppDbContext.OnModelCreating`:
```csharp
modelBuilder.Entity<Spool>().Property(s => s.CreatedAt)
    .HasDefaultValueSql("datetime('now')");
```

---

### IN-02: `ColorHex` field in `index.html` uses `pattern` attribute that is not enforced on submit because `novalidate` is set

**File:** `FilamentCatalog/wwwroot/index.html:96` and `83`
**Issue:** The `<input type="text" id="spool-color-hex" pattern="^#[0-9A-Fa-f]{6}$">` has a regex constraint, but the `<form id="spool-form" novalidate>` attribute suppresses all native browser validation. The pattern is therefore purely decorative and provides no protection. The JS `getColorHex()` helper does validate the value (returning `#888888` as fallback), so there is no functional bug, but the pattern attribute misleads readers into thinking browser validation is active.

**Fix:** Either remove the `novalidate` attribute (and handle the `invalid` event on the form), or remove the `pattern` attribute from the color hex input since `getColorHex()` already handles it in JS.

---

### IN-03: `console.error` left in production path

**File:** `FilamentCatalog/wwwroot/js/app.js:39` and `53`
**Issue:** `console.error('Failed to refresh after spool mutation:', err)` and `console.error('Failed to refresh after owner mutation:', err)` will log stack traces to the browser console in production. For a local-only app this is low-severity, but it leaks internal error details that could assist an attacker if the app is ever exposed beyond localhost.

**Fix:** Either suppress these in a production build or surface the error to the user via a toast/banner rather than logging to the console. At minimum, log `err.message` rather than the full error object.

---

_Reviewed: 2026-05-01T09:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
