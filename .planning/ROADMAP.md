# Roadmap: Filament Catalog

## Overview

Three phases deliver a working local filament inventory app. Phase 1 lays the runnable foundation — Windows service, SQLite, static file serving, and the "Me" owner seed. Phase 2 builds every user-facing feature on top of that foundation: add/edit/delete spools, owner management, filters, and the summary/balance views. Phase 3 connects the app to the Bambu Lab EU catalog so the user can populate the product list by syncing instead of typing, completing the core value.

## Phases

- [x] **Phase 1: Foundation** - Runnable Windows service with data layer, EF Core migrations, and seeded "Me" owner *(completed 2026-05-01)*
- [ ] **Phase 2: Spool & Owner CRUD** - Full spool management, owner management, and summary/balance views in the browser
- [ ] **Phase 3: Bambu Catalog Sync** - Shopify JSON API sync with ImageSharp color extraction and sync-status UI
- [x] **Phase 4: Refactor Project Structure** - Split EF Core layer into FilamentCatalog.EntityFramework project, rename main project to FilamentCatalog.Service, and extract API endpoints from Program.cs into organized service/controller classes with proper DI *(completed 2026-05-01)*

## Phase Details

### Phase 1: Foundation
**Goal**: The app starts as a Windows service, serves a placeholder page at localhost:5000, and has a fully migrated SQLite database with a seeded "Me" owner.
**Depends on**: Nothing (first phase)
**Requirements**: INFRA-01, INFRA-02, INFRA-03, OWNER-03
**Success Criteria** (what must be TRUE):
  1. Running `sc start FilamentCatalog` (or the service auto-starting) makes the app accessible at http://localhost:5000 in a browser
  2. `filament.db` is created next to the executable (AppContext.BaseDirectory), not in the working directory
  3. The database contains the correct tables and one Owner row with IsMe = true and Name = "Me"
  4. Navigating to http://localhost:5000 serves index.html without a 404
**Plans**: 3 plans

**Wave 1:**
- [x] 01-01-PLAN.md — Project scaffold (csproj, appsettings.json, Owner model, AppDbContext, wwwroot/index.html)

**Wave 2** *(blocked on Wave 1 completion)*:
- [x] 01-02-PLAN.md — Program.cs + EF migration + app startup verification
- [x] 01-03-PLAN.md — Service install/uninstall scripts + human smoke test

**Cross-cutting constraints:**
- `Path.Combine(AppContext.BaseDirectory, "filament.db")` — all plans referencing the DB path must use this exact form
- `UseDefaultFiles()` before `UseStaticFiles()` — middleware order is non-negotiable
- Stale `__EFMigrationsLock` guard must execute before `MigrateAsync()`
- `DateTime.UtcNow` everywhere — no `DateTime.Now`

**UI hint**: no

### Phase 2: Spool & Owner CRUD
**Goal**: Users can add, edit, delete, and filter spools; manage owners; and see totals and per-owner balances — all from the browser.
**Depends on**: Phase 1
**Requirements**: SPOOL-01, SPOOL-02, SPOOL-03, SPOOL-04, SPOOL-05, SPOOL-06, OWNER-01, OWNER-02, BAL-01, BAL-02, BAL-03
**Success Criteria** (what must be TRUE):
  1. User can open the app, add a spool (free-text name/material/color in Phase 2), assign it to an owner, set weight/price/statuses, and see it appear in the spool list
  2. User can edit a spool's fields and delete a spool; deleting an owner with spools shows an error
  3. User can filter the spool list by owner, material type, spool status, payment status, and free text — combinations work
  4. Summary bar shows correct totals (spools, my spools, total value, total owed); balance table shows one row per non-me owner with spool count, value, and amount owed; rows with price-missing spools are visually flagged
**Plans**: 4 plans

**Wave 1** *(parallel — no shared files)*:
- [x] 02-01-PLAN.md — Spool entity + PaymentStatus/SpoolStatus enums + AppDbContext extension + AddSpools migration
- [x] 02-03-PLAN.md — index.html (full page markup) + app.css (design tokens + all component styles) + api.js (fetch wrappers)

**Wave 2** *(blocked on 02-01)*:
- [x] 02-02-PLAN.md — All 9 API endpoints in Program.cs (owners CRUD, spools CRUD, summary, balance) + JsonStringEnumConverter config

**Wave 3** *(blocked on 02-02 + 02-03)*:
- [x] 02-04-PLAN.md — JS feature modules: spools.js (render + filter + dialog) + owners.js (modal) + summary.js (stats + balance) + app.js (init + wiring) + human verify checkpoint

**Cross-cutting constraints:**
- `DateTime.UtcNow` everywhere — no `DateTime.Now`
- `DeleteBehavior.Restrict` on Spool→Owner FK — no cascade delete
- ColorHex defaults to `#888888` if empty/invalid (enforced server-side in POST and PUT)
- Enum values serialize as strings via `JsonStringEnumConverter` — JS filter depends on "Sealed"/"Active"/"Empty" strings
- Native `<dialog>` for all modals — no div overlay
- ES modules with `type="module"` — no bundler, no framework
- All user-supplied strings rendered to DOM via `textContent` — no innerHTML with user data (XSS)

**UI hint**: yes

### Phase 3: Bambu Catalog Sync
**Goal**: Users can sync the Bambu Lab EU product catalog from within the app; synced products populate the spool-creation dropdown, and the app works fully offline after the first sync.
**Depends on**: Phase 2
**Requirements**: SYNC-01, SYNC-02, SYNC-03, SYNC-04, SYNC-05, SYNC-06
**Success Criteria** (what must be TRUE):
  1. Clicking "Sync Bambu catalog" triggers a background sync; the UI shows a progress indicator while running (202 + polling /api/sync/status) and the last-synced timestamp once complete
  2. After sync, the spool-creation dropdown is populated with Bambu products including their extracted dominant color swatches
  3. Re-running sync upserts products without duplicates (matched on Name + Material) and updates LastSyncedAt
  4. After the first successful sync, disconnecting from the internet and reloading the app still shows full product data in the dropdown
**Plans**: TBD
**UI hint**: yes

### Phase 4: Refactor Project Structure
**Goal**: The solution is split into two projects (FilamentCatalog.EntityFramework and FilamentCatalog.Service), API endpoints are organized into service/controller classes outside of Program.cs, and DI is used throughout.
**Depends on**: Phase 3
**Requirements**: TBD
**Success Criteria** (what must be TRUE):
  1. A separate FilamentCatalog.EntityFramework project contains all EF Core models, DbContext, and migrations; the service project references it
  2. The main project is renamed FilamentCatalog.Service and Program.cs contains only app bootstrapping (no inline endpoint handlers)
  3. API endpoints are organized into dedicated service or controller classes with constructor-injected dependencies
  4. The app still builds, runs as a Windows service, and all existing features work correctly after refactor
**Plans**: 3 plans

**Wave 1:**
- [x] 04-01-PLAN.md — Create FilamentCatalog.EntityFramework project (class library, copy models + AppDbContext + migrations)

**Wave 2** *(blocked on 04-01)*:
- [x] 04-02-PLAN.md — Rename FilamentCatalog → FilamentCatalog.Service, update solution file, add ProjectReference, delete duplicate EF artifacts, verify build

**Wave 3** *(blocked on 04-02)*:
- [x] 04-03-PLAN.md — Service layer (IOwnerService, ISpoolService, ISummaryService) + [ApiController] controllers (OwnersController, SpoolsController, SummaryController, BalanceController) + domain exceptions; Program.cs wires DI + app.MapControllers()

**Cross-cutting constraints:**
- All endpoint handler bodies copied verbatim — no logic changes during refactor
- No namespace declarations (consistent with existing codebase style using implicit global namespace)
- `UseDefaultFiles()` before `UseStaticFiles()` — middleware order preserved in rewritten Program.cs

**UI hint**: no

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 3/3 | Complete | 2026-05-01 |
| 2. Spool & Owner CRUD | 4/4 | In progress | - |
| 3. Bambu Catalog Sync | 0/? | Not started | - |
| 4. Refactor Project Structure | 3/3 | Complete | 2026-05-01 |
