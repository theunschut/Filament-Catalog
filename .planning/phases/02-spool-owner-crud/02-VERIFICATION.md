---
phase: 02-spool-owner-crud
verified: 2026-05-01T00:00:00Z
status: human_needed
score: 4/4 roadmap success criteria verified
overrides_applied: 0
human_verification:
  - test: "Add a spool via the browser UI and verify it appears in the list"
    expected: "Spool row renders with color swatch, name, material, owner name, weight, price, status badges, notes icon, and Edit button"
    why_human: "DOM rendering and visual layout requires a browser; automated grep confirms code path exists but cannot verify rendered output"
  - test: "Edit a spool row — open dialog pre-populated, change payment status to Paid, save"
    expected: "Dialog opens with all fields populated from spool data; after save, balance table updates and owed amount decreases for that owner"
    why_human: "Form population and live balance update require interaction in a running browser"
  - test: "Use the chip filters in combination (e.g. Sealed + Unpaid) then type in search box"
    expected: "Only spools matching ALL active filters are shown (AND logic); deselecting a chip re-shows spools matching remaining filters"
    why_human: "Filter AND logic and live DOM hide/show behavior requires interactive testing"
  - test: "Delete an owner who has spools assigned"
    expected: "Inline error appears in the owner modal: 'Cannot delete — N spool(s) assigned. Remove spools first.' Modal stays open."
    why_human: "Error display and modal stay-open behavior requires running app interaction"
  - test: "Add a spool with no price set; check the balance section"
    expected: "Balance row for that owner shows a ⚠ icon after the owner name with tooltip 'One or more spools have no price — totals may be incomplete.'"
    why_human: "BAL-03 warning flag is DOM-appended at runtime; requires visual/browser verification"
  - test: "Backdrop click on both dialogs (spool dialog, owner dialog)"
    expected: "Clicking outside the modal content area closes the dialog (native <dialog> backdrop behavior)"
    why_human: "Native dialog backdrop click requires browser interaction to confirm the e.target === dialog check fires correctly"
---

# Phase 2: Spool & Owner CRUD — Verification Report

**Phase Goal:** Users can add, edit, delete, and filter spools; manage owners; and see totals and per-owner balances — all from the browser.
**Verified:** 2026-05-01
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can add a spool (free-text name/material/color), assign it to an owner, set weight/price/statuses, and see it in the spool list | VERIFIED | `Program.cs` POST /api/spools exists with full field handling and ColorHex default; `spools.js` `buildSpoolRow` renders all fields; `app.js` calls `renderSpools` after every dialog close |
| 2 | User can edit a spool's fields and delete a spool; deleting an owner with spools shows an error | VERIFIED | PUT /api/spools/{id:int} updates all mutable fields; DELETE /api/spools/{id:int} removes spool; DELETE /api/owners/{id:int} returns 409 Conflict with `{ error: "..." }` when spoolCount > 0; `spools.js` inline delete confirm flow wired |
| 3 | User can filter the spool list by owner, material type, spool status, payment status, and free text — combinations work | VERIFIED | `applyFilters()` in `spools.js` applies AND logic across all five filters using `spoolStatusFilter.size > 0` empty-set-means-all guard; all five inputs wired with event listeners in `initChipFilters()` |
| 4 | Summary bar shows correct totals; balance table shows one row per non-me owner with spool count, value, amount owed; rows with price-missing spools are visually flagged | VERIFIED | GET /api/summary returns `{totalSpools, mySpools, totalValue, totalOwed}` with Me-owner exclusion from owed; GET /api/balance excludes Me owner via `!o.IsMe` filter; `summary.js` `renderBalance` appends `⚠` span when `row.hasUnpriced === true` |

**Score:** 4/4 roadmap success criteria verified

### SPOOL-01 Partial Scope Note

SPOOL-01 in REQUIREMENTS.md reads: "User can add a spool by selecting a product from a searchable catalog dropdown (color swatch preview shown on selection)."

Phase 2 ROADMAP SC1 explicitly scopes this: "add a spool (free-text name/material/color in Phase 2)". The catalog dropdown is a Phase 3 deliverable (Bambu Catalog Sync). Phase 2 implements free-text name/material/color entry, which is the correct Phase 2 scope. SPOOL-01 will be fully satisfied only after Phase 3 completes.

This is not a gap — it is an intentionally deferred portion of SPOOL-01. The Phase 2 portion of SPOOL-01 (adding spools manually with all fields) is fully implemented.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `FilamentCatalog/Models/Spool.cs` | Spool entity with all D-02 fields | VERIFIED | 14-line file with all 13 properties: Id, Name, Material, ColorHex, OwnerId, Owner, WeightGrams, PricePaid, PaymentStatus, SpoolStatus, Notes, CreatedAt; no namespace declaration |
| `FilamentCatalog/Models/PaymentStatus.cs` | PaymentStatus enum | VERIFIED | `public enum PaymentStatus { Paid, Unpaid, Partial }` |
| `FilamentCatalog/Models/SpoolStatus.cs` | SpoolStatus enum | VERIFIED | `public enum SpoolStatus { Sealed, Active, Empty }` |
| `FilamentCatalog/AppDbContext.cs` | DbContext with Spool DbSet + FK config | VERIFIED | Contains `DbSet<Spool>` and `OnModelCreating` with `DeleteBehavior.Restrict` |
| `FilamentCatalog/Migrations/20260501070939_AddSpools.cs` | EF Core migration creating Spools table | VERIFIED | `CreateTable("Spools", ...)` with all columns; `ReferentialAction.Restrict` FK to Owners |
| `FilamentCatalog/Program.cs` | All 9 API endpoints registered | VERIFIED | All Map* calls present before `await app.RunAsync()`; `JsonStringEnumConverter` configured |
| `FilamentCatalog/wwwroot/index.html` | Complete page structure with all required IDs | VERIFIED | All 28+ required element IDs present; native `<dialog>` for both modals; `<details open>` for balance; `<script type="module">` entry point |
| `FilamentCatalog/wwwroot/css/app.css` | Full design token system and component styles | VERIFIED | `--color-accent: #2563eb`; all badge classes present; `#summary-bar` with `position: sticky; z-index: 100` |
| `FilamentCatalog/wwwroot/js/api.js` | 9 fetch wrapper functions | VERIFIED | 9 named async exports: getOwners, createOwner, deleteOwner, getSpools, createSpool, updateSpool, deleteSpool, getSummary, getBalance; no imports (leaf module) |
| `FilamentCatalog/wwwroot/js/summary.js` | Summary bar + balance table rendering | VERIFIED | Exports renderSummary, renderBalance, refreshSummaryAndBalance; BAL-03 flag via `hasUnpriced` check; Intl.NumberFormat de-DE EUR; textContent used throughout |
| `FilamentCatalog/wwwroot/js/owners.js` | Owner modal logic | VERIFIED | Exports initOwnerModal; CustomEvent('owners-updated') on dialog close; backdrop click handler; Me-owner has no delete button |
| `FilamentCatalog/wwwroot/js/spools.js` | Spool list + filter + dialog | VERIFIED | Exports renderSpools, initSpoolDialog, initChipFilters, onSpoolDialogClose, repopulateOwnerFilter, repopulateOwnerSelect; Set-based chip filter with empty-set-means-all |
| `FilamentCatalog/wwwroot/js/app.js` | Page-load init orchestrator | VERIFIED | Imports from all 4 modules; top-level await Promise.all; wires spool dialog close and owners-updated event |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `AppDbContext.cs` | `Spool.cs` | `DbSet<Spool>` + `OnDelete(DeleteBehavior.Restrict)` | WIRED | Confirmed in file; migration generated with `ReferentialAction.Restrict` |
| `Spool.cs` | `Owner.cs` | `public int OwnerId` + `public Owner Owner { get; set; } = null!` | WIRED | FK and navigation property present |
| `Program.cs` | `AppDbContext.cs` | `async (AppDbContext db)` in every endpoint lambda | WIRED | Confirmed in all 9 endpoint lambdas |
| GET /api/balance | IsMe owner filter | `!o.IsMe` in LINQ Where | WIRED | Line 158 of Program.cs: `db.Owners.Where(o => !o.IsMe)` |
| `app.js` | `api.js` | `import { getSpools, getOwners, getSummary, getBalance } from './api.js'` | WIRED | Line 3 of app.js |
| `spools.js` | `#spool-list` in index.html | `document.getElementById('spool-list')` | WIRED | Line 14 of spools.js; `listEl.replaceChildren(fragment)` in renderSpools |
| `owners.js` | `#owner-dialog` | `dialog.showModal()` and `dialog.close()` | WIRED | Lines 68 and 72 of owners.js |
| `owners.js` | `app.js` | `CustomEvent('owners-updated')` dispatched on `document` | WIRED | Line 76 of owners.js; `document.addEventListener('owners-updated', ...)` in app.js line 44 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `spools.js` `renderSpools` | `allSpools` / `spools` param | `getSpools()` → GET /api/spools → `db.Spools.Include(s => s.Owner).OrderBy(...)` | Yes — EF Core DB query | FLOWING |
| `summary.js` `renderSummary` | `summary` param | `getSummary()` → GET /api/summary → EF Core aggregate over `db.Spools` | Yes — EF Core query with arithmetic | FLOWING |
| `summary.js` `renderBalance` | `rows` param / `balance` | `getBalance()` → GET /api/balance → `db.Owners.Where(!IsMe)` + `db.Spools` | Yes — EF Core query with per-owner grouping | FLOWING |
| `owners.js` `renderOwnerList` | `owners` from `getOwners()` | GET /api/owners → `db.Owners.OrderBy(o => o.CreatedAt)` | Yes — EF Core DB query | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build exits 0 | `dotnet build FilamentCatalog/` | `Build succeeded. 0 Warning(s) 0 Error(s)` | PASS |
| Migration file has Restrict FK | `grep ReferentialAction.Restrict Migrations/20260501070939_AddSpools.cs` | `onDelete: ReferentialAction.Restrict` at line 38 | PASS |
| api.js exports 9 functions | Count `export async function` in api.js | 9 matches | PASS |
| DeleteBehavior.Restrict in context | `grep DeleteBehavior.Restrict AppDbContext.cs` | Found at line 16 | PASS |
| Enum string serialization configured | `grep JsonStringEnumConverter Program.cs` | Found at line 33 | PASS |
| ColorHex defaults to #888888 | `grep "#888888" Program.cs` | Found in POST (line 88) and PUT (line 120) handlers | PASS |
| DateTime.UtcNow used | `grep DateTime.UtcNow Program.cs` | Found in POST /api/spools (line 100) and POST /api/owners | PASS |
| UseDefaultFiles before UseStaticFiles | Line order in Program.cs | UseDefaultFiles line 45, UseStaticFiles line 46 — correct order | PASS |
| Empty Set = show all in chip filters | `grep "spoolStatusFilter.size > 0" spools.js` | Found at line 68 | PASS |

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SPOOL-01 | 02-01, 02-02, 02-03, 02-04 | Add spool (Phase 2: free-text; Phase 3: catalog dropdown) | PARTIAL — Phase 2 portion satisfied | Free-text add via POST /api/spools + spool dialog implemented; catalog dropdown deferred to Phase 3 per ROADMAP |
| SPOOL-02 | 02-01, 02-02, 02-03, 02-04 | Assign spool to owner | SATISFIED | `OwnerId` field in Spool entity; `#spool-owner` select in dialog; POST/PUT validate owner exists |
| SPOOL-03 | 02-01, 02-02, 02-03, 02-04 | Set weight, price, payment status, spool status, notes | SATISFIED | All fields in Spool entity, POST/PUT handlers, and spool dialog form |
| SPOOL-04 | 02-02, 02-04 | Edit spool fields | SATISFIED | PUT /api/spools/{id:int} + `populateFormForEdit` pre-populates all form fields |
| SPOOL-05 | 02-02, 02-04 | Delete spool | SATISFIED | DELETE /api/spools/{id:int} + inline confirm flow in spools.js |
| SPOOL-06 | 02-03, 02-04 | Filter by owner, material, spool status, payment status, free text | SATISFIED | `applyFilters()` with 5-dimensional AND logic; all filter inputs wired |
| OWNER-01 | 02-02, 02-04 | Add named owner | SATISFIED | POST /api/owners with 422 guard; owner modal add form |
| OWNER-02 | 02-02, 02-04 | Delete owner — rejected if owner has spools | SATISFIED | DELETE /api/owners/{id:int} returns 409 when spoolCount > 0; error displayed inline in owner modal |
| BAL-01 | 02-02, 02-03, 02-04 | Summary bar with total spools, my spools, total value, total owed | SATISFIED | GET /api/summary + `renderSummary` in summary.js updates all four stat spans |
| BAL-02 | 02-02, 02-03, 02-04 | Balance table per non-me owner | SATISFIED | GET /api/balance excludes Me owner; `renderBalance` renders one row per non-me owner |
| BAL-03 | 02-02, 02-03, 02-04 | Visual flag for owners with unpriced spools | SATISFIED | `hasUnpriced` field in balance API response; `renderBalance` appends ⚠ span with tooltip when true |

**Orphaned requirements check:** OWNER-03 (seed Me owner) is assigned to Phase 1 in REQUIREMENTS.md traceability and was completed in Phase 1 — not orphaned in Phase 2.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None | — | — | — | — |

No TODO/FIXME/placeholder comments found. No stub return patterns found. No hardcoded empty data that flows to rendering. Build exits 0 with 0 warnings.

One observation: in `spools.js` line 71, template literal `${spool.name} ${spool.material} ${spool.notes ?? ''}` is used for filter text construction in `applyFilters()`. This is used for `.toLowerCase().includes()` comparison, NOT for DOM injection — this is safe and is not XSS-relevant.

### Human Verification Required

#### 1. Add Spool End-to-End Flow

**Test:** Navigate to http://localhost:5000. Click "Add Spool". Fill in: Name "Test PLA", Material "PLA", select a color, choose owner "Me", Weight 1000, Price 19.99, status Sealed, payment Unpaid. Click "Save Spool".
**Expected:** Dialog closes. Spool row appears with color swatch, "Test PLA", "PLA" text, "Me" owner name, "1000g", "€19,99", Sealed badge, Unpaid badge, and Edit button. Summary bar Total Spools increments to 1, My Spools to 1.
**Why human:** DOM rendering, CSS layout, and badge color correctness require visual browser verification.

#### 2. Edit Spool Pre-population

**Test:** Click "Edit" on an existing spool row.
**Expected:** Dialog opens titled "Edit Spool" with all form fields pre-populated from the spool's stored data. Delete button is visible. Color swatch shows the spool's color.
**Why human:** Form field pre-population and visual correctness require interactive browser testing.

#### 3. Filter AND Logic

**Test:** With at least one Sealed/Unpaid spool and one Active/Paid spool in the list: click "Sealed" chip, then click "Unpaid" chip.
**Expected:** Only spools that are BOTH Sealed AND Unpaid are shown. Clicking "Sealed" again deselects it — now only Unpaid spools show (Active+Paid spool still hidden). Clearing all chips shows all spools.
**Why human:** Set-based chip filter toggle and AND logic require interactive verification with real data.

#### 4. Owner Delete 409 Guard

**Test:** Add a spool assigned to a non-Me owner. Open owner modal, click Delete next to that owner.
**Expected:** Inline error appears in the modal body: "Cannot delete — 1 spool(s) assigned. Remove spools first." Modal stays open — does not close.
**Why human:** Error display and modal-stays-open behavior requires running app with real data.

#### 5. BAL-03 Unpriced Warning Flag

**Test:** Add a spool for a non-Me owner with no price set. Check the Balance Overview section.
**Expected:** Owner row shows ⚠ character after the owner name with a tooltip reading "One or more spools have no price — totals may be incomplete."
**Why human:** DOM-appended icon and tooltip text require visual browser inspection.

#### 6. Backdrop Click and Escape Key

**Test:** Open the spool dialog. Click the backdrop (outside the modal). Open the owner dialog. Press Escape key.
**Expected:** Both actions close the respective dialog (native `<dialog>` behavior). No JS errors in the console.
**Why human:** Native `<dialog>` backdrop and keyboard behavior requires interactive testing.

### Gaps Summary

No programmatically verifiable gaps found. All 4 roadmap success criteria are VERIFIED in code. All 13 required artifacts exist with substantive implementation and correct wiring. All data flows traced to live EF Core queries. Build passes clean.

The only unresolved items are the 6 human verification tests above, which require a running browser session to confirm visual rendering, interactive behaviors, and live data flows.

**SPOOL-01 catalog dropdown scope:** The partial implementation of SPOOL-01 (free-text vs catalog dropdown) is intentional per the ROADMAP Phase 2 scope definition and is not a gap — the Phase 3 goal explicitly delivers the catalog dropdown.

---

_Verified: 2026-05-01_
_Verifier: Claude (gsd-verifier)_
