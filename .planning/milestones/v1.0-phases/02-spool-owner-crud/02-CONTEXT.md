# Phase 2: Spool & Owner CRUD - Context

**Gathered:** 2026-05-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can add, edit, and delete spools; manage owners; filter the spool list; and see a summary bar and per-owner balance table — all from the browser. Phase 2 builds every user-facing feature on top of the Phase 1 foundation (service, SQLite, static file serving).

Requirements in scope: SPOOL-01 through SPOOL-06, OWNER-01, OWNER-02, BAL-01, BAL-02, BAL-03

Phase 3 (Bambu Catalog Sync) is out of scope. BambuProduct table does not exist in Phase 2. The spool-add form uses free-text entry for product info; the catalog dropdown is wired up in Phase 3.

</domain>

<decisions>
## Implementation Decisions

### Spool data model
- **D-01:** Hybrid model — `Spool` stores `Name`, `Material`, and `ColorHex` as its own non-nullable columns (always self-describing). A nullable `BambuProductId` FK column is NOT added in Phase 2 — that FK is added by Phase 3's migration when the catalog is available. Phase 2 spool-add/edit form uses free-text inputs for name, material, and a color hex picker.
- **D-02:** The Spool entity in Phase 2 has: `Id`, `Name` (string, required), `Material` (string, required), `ColorHex` (string, required — default to #888888 if empty), `OwnerId` (FK to Owner), `WeightGrams` (int?, optional), `PricePaid` (decimal?, optional), `PaymentStatus` (enum: Paid/Unpaid/Partial), `SpoolStatus` (enum: Sealed/Active/Empty), `Notes` (string?, optional), `CreatedAt` (DateTime UTC).
- **D-03:** `OwnerId` is a required FK to `Owner`. Deleting an owner with spools returns a 409 error — no cascade delete.

### Page layout
- **D-04:** Single HTML page with a sticky summary bar at the top (always visible on scroll). Main body: horizontal filter bar above the spool list. Below the spool list: a collapsible section containing the balance table. Owner management is in a separate settings modal (see D-07), not in this collapsible section.
- **D-05:** The collapsible balance section starts expanded by default. Toggle state does not need to persist across reloads.

### Filter UX
- **D-06:** Horizontal filter bar above the spool list. Five filters:
  - **Owner**: `<select>` dropdown (populated from API, includes "All owners" default)
  - **Material**: `<select>` dropdown (populated from distinct materials in the spool list)
  - **Spool status**: chip buttons — Sealed / Active / Empty (all deselected = show all)
  - **Payment status**: chip buttons — Paid / Unpaid / Partial (all deselected = show all)
  - **Free text**: `<input type="search">` — matches against spool name, material, notes
  - Filtering is live/instant on every change — no Apply button needed.

### Owner management
- **D-07:** Owner CRUD lives in a native `<dialog>` modal opened by a gear/settings icon in the page header. Fetches the owner list on open. On close, dispatches a custom event to refresh the owner `<select>` dropdown in the spool-add/edit form. "Me" (IsMe = true) is displayed but cannot be deleted. Deleting an owner with spools shows an inline error message in the modal (do not close the modal on error).

### API endpoints
- **D-08:** Minimal API endpoints needed: `GET/POST /api/owners`, `DELETE /api/owners/{id}`, `GET/POST /api/spools`, `GET/PUT/DELETE /api/spools/{id}`, `GET /api/summary` (returns summary bar data), `GET /api/balance` (returns per-owner balance rows).
- **D-09:** All responses use JSON. No authentication. Errors return structured JSON `{ "error": "..." }` with appropriate HTTP status codes (404, 409, 422 as needed).

### Balance calculation
- **D-10:** Amount owed = sum of `PricePaid` for spools assigned to a non-me owner where `PaymentStatus` is Unpaid or Partial. Spools with null `PricePaid` contribute 0 to the sum but cause the balance row to be visually flagged (BAL-03).
- **D-11:** Summary bar totals: total spool count, spool count where OwnerId = "Me" owner, total value (sum of all non-null PricePaid), total owed (sum of owed amounts across all non-me owners).

### Claude's Discretion
- Exact HTML/CSS styling, color scheme, and visual design — keep it clean and functional, no specific design system required
- HTTP status code choices within the ranges noted in D-09
- Whether to use CSS custom properties for colors
- Exact chip button styling (border, active color treatment)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Critical conventions
- `CLAUDE.md` — SQLite path (`AppContext.BaseDirectory`), static file middleware order (`UseDefaultFiles` before `UseStaticFiles`), `DateTime.UtcNow` requirement, native `<dialog>` for modals, ES modules for JS

### Requirements
- `.planning/REQUIREMENTS.md` §Spool Management — SPOOL-01 through SPOOL-06
- `.planning/REQUIREMENTS.md` §Owner Management — OWNER-01, OWNER-02
- `.planning/REQUIREMENTS.md` §Summary & Balance — BAL-01, BAL-02, BAL-03
- `.planning/REQUIREMENTS.md` §v2 Requirements (deferred) — do NOT implement these in Phase 2

### Project context
- `.planning/PROJECT.md` — Stack constraints, out-of-scope items, core value
- `.planning/phases/01-foundation/01-CONTEXT.md` — Phase 1 decisions (logging, migrations pattern, Owner model)

### Existing code
- `FilamentCatalog/Models/Owner.cs` — existing Owner entity (Id, Name, IsMe, CreatedAt)
- `FilamentCatalog/AppDbContext.cs` — existing context; Phase 2 adds Spool DbSet and new migration
- `FilamentCatalog/Program.cs` — existing startup; Phase 2 adds API endpoint registrations here

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Owner` model (Id, Name, IsMe, CreatedAt) — used as-is; Phase 2 adds Spool that FK-references it
- `AppDbContext` — extend with `DbSet<Spool>` and add EF Core migration for the Spool table
- `Program.cs` startup pattern — `ClearStaleEfMigrationsLock` + `MigrateAsync` + `SeedAsync` already wired; Phase 2 just adds `MapGet`/`MapPost` etc. calls before `app.Run()`
- Serilog logging — already configured; Phase 2 inherits it for free

### Established Patterns
- EF Core migrations: one migration per phase (following D-02 from Phase 1 context)
- `DateTime.UtcNow` everywhere — no exceptions
- Static files from `wwwroot/` — `index.html` is the entry point; Phase 2 replaces/extends it with the full UI
- ES modules (`type="module"`) for JS — no bundler, import from relative paths

### Integration Points
- Phase 2 replaces the placeholder `wwwroot/index.html` with the full single-page UI
- New API endpoints registered in `Program.cs` before `app.RunAsync()`
- Phase 3 will add a `BambuProduct` entity and a nullable `Spool.BambuProductId` FK via its own migration — Phase 2's Spool schema must not assume this FK exists

</code_context>

<specifics>
## Specific Ideas

- Color hex input for spool: a simple `<input type="color">` or text input for the hex value; display a small color swatch preview next to it in the form
- Balance row flagging (BAL-03): a visual indicator (e.g., asterisk, warning icon, or muted italic text) when one or more of the owner's spools have no price set

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 02-spool-owner-crud*
*Context gathered: 2026-05-01*
