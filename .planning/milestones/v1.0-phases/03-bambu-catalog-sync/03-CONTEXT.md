# Phase 3: Bambu Catalog Sync - Context

**Gathered:** 2026-05-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Connect the app to the Bambu Lab EU Shopify catalog so users can sync products in the background, then pick a filament from a two-step dropdown when adding a spool. Color is extracted from Shopify swatch images via ImageSharp. After the first sync the app works fully offline — BambuProduct table is the source of truth.

</domain>

<decisions>
## Implementation Decisions

### Product Picker (Add Spool Dialog)
- **D-01:** Two-step picker: first `<select>` lists distinct materials (e.g. PLA, ABS, PETG), second `<select>` lists color variants for the selected material. No custom combobox overlay — both steps use plain native `<select>` elements.
- **D-02:** When a color variant is selected, the spool Name auto-fills as `"Product title — Color name"` (e.g. `"Bambu Lab PLA Basic — Bambu White"`). Name field stays editable so the user can shorten it.
- **D-03:** Edit and duplicate dialogs restore the two-step selects — material pre-selected, then color pre-selected — so the user can re-pick from the catalog when editing. The restoration uses the saved spool's Material and Name fields to match against catalog data.

### Color Field Behavior
- **D-04:** When a color variant is picked, ColorHex auto-fills from the catalog's extracted value, but the field stays editable (not locked, not hidden). Lets the user correct any ImageSharp extraction errors for gradient or multi-color swatches.

### Pre-Sync State (Empty Catalog)
- **D-05:** When the BambuProduct table is empty (never synced), the "Add Spool" button is disabled and a short inline banner tells the user to sync first. The button re-enables reactively after the first sync completes — no page reload required. A module-level flag or a quick `GET /api/catalog/count` check on page load drives this.

### Sync UI Placement
- **D-06:** The "Sync Bambu catalog" button and "Last synced: X" timestamp live in the sticky header, grouped near the existing gear icon. The timestamp renders using the established `.stat-cell` pattern so it visually fits alongside the summary stats.

### Already-Locked Decisions (carry from CLAUDE.md)
- Shopify JSON API (`/products.json`) — no HTML scraping
- ImageSharp center-crop + filter alpha < 128 for dominant color extraction; always dispose with `using`
- BackgroundService + Channel<SyncJob> (capacity 1, DropNewest) for background sync
- SyncStateService singleton exposes sync state to the API
- Sync progress via 202 + polling `/api/sync/status` — NOT SSE
- Upsert key: Name + Material (per SYNC-04) — researcher should verify how "Name" maps to Shopify product/variant fields
- BambuProduct table is source of truth after first sync (app works offline)

### Claude's Discretion
- Exact BambuProduct entity fields beyond Name, Material, ColorName, ColorHex, LastSyncedAt — researcher should inspect the Shopify `/products.json` response shape and decide what's worth storing
- Whether Spool stores a FK to BambuProduct or just copies the data at creation time — planner should decide based on restore-two-step-selects requirement (D-03)
- Sync status polling interval and what the status response body contains (progress count, percentage, error message)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Requirements
- `.planning/REQUIREMENTS.md` — SYNC-01 through SYNC-06 define the full sync requirements; SPOOL-01 defines the searchable catalog dropdown requirement
- `.planning/ROADMAP.md` §Phase 3 — success criteria (4 items) and cross-cutting constraints

### Architecture & Conventions
- `CLAUDE.md` — critical conventions: SQLite path, middleware order, BackgroundService + Channel pattern, SyncStateService, 202+polling, ImageSharp disposal, DateTime.UtcNow

### Existing Code (integration points)
- `FilamentCatalog.EntityFramework/AppDbContext.cs` — add BambuProduct DbSet here; migration lives in this project
- `FilamentCatalog.EntityFramework/Models/Spool.cs` — Spool entity; planner must decide whether to add a BambuProductId FK
- `FilamentCatalog.Service/wwwroot/js/spools.js` — Add/Edit/Duplicate dialog logic lives here; two-step picker and restore behavior go in this file
- `FilamentCatalog.Service/wwwroot/js/api.js` — add catalog API fetch wrappers here
- `FilamentCatalog.Service/wwwroot/index.html` — header section (sticky, contains gear icon + stat cells); spool-dialog markup for two-step selects
- `FilamentCatalog.Service/wwwroot/app.css` — design tokens and component styles; two-step picker and sync-status styles go here

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `.stat-cell` pattern (index.html + app.css): used for summary stats in the sticky header — "Last synced: X" timestamp reuses this pattern for the sync status display
- `repopulateOwnerSelect` / `repopulateOwnerFilter` pattern (spools.js): shows how to rebuild a `<select>` from data — same approach for populating material and color selects
- Native `<dialog>` modal pattern (spools.js + index.html): all modals use this; new catalog-related dialogs or state must follow the same `showModal()` / `close()` lifecycle
- `JsonStringEnumConverter` already configured — enum values serialize as strings (relevant to sync status enum if one is introduced)

### Established Patterns
- ES modules with `type="module"` — no bundler; new JS files must be proper ES modules with named exports
- `textContent` only for user-supplied data — no innerHTML with untrusted strings (XSS)
- `DateTime.UtcNow` everywhere — no `DateTime.Now`
- Controller-based API (`[ApiController]`) — new sync endpoints go in a dedicated `SyncController.cs`; service interface + implementation follow the `IOwnerService` / `OwnerService` pattern
- `AppContext.BaseDirectory` for all file paths — relevant if sync caches anything to disk (unlikely, but flag it)

### Integration Points
- Sync background service wires into `Program.cs` via `AddHostedService<>()` — existing pattern from CLAUDE.md
- `Channel<SyncJob>` producer lives on a new `POST /api/sync/start` endpoint; consumer is the BackgroundService
- Two-step selects in the Add Spool dialog replace the current free-text `nameInput` and `matInput` fields — these DOM refs in spools.js will change
- Sticky header in index.html receives the sync button + stat cell — check current flex layout before adding elements

</code_context>

<specifics>
## Specific Ideas

- "Last synced: X" rendered as a `.stat-cell` in the sticky header, near the gear icon — matches existing visual language
- Two-step picker: first `<select id="spool-catalog-material">`, second `<select id="spool-catalog-color">` — material change event rebuilds the color select
- Name auto-fill format: `"${productTitle} — ${colorName}"` — user can shorten in the Name input
- Disable state for pre-sync: `addBtn.disabled = true` + a `<p id="catalog-empty-notice">` banner; remove both once catalog count > 0

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 3-Bambu Catalog Sync*
*Context gathered: 2026-05-02*
