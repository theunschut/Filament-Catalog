# Roadmap: Filament Catalog

## Overview

Three phases deliver a working local filament inventory app. Phase 1 lays the runnable foundation — Windows service, SQLite, static file serving, and the "Me" owner seed. Phase 2 builds every user-facing feature on top of that foundation: add/edit/delete spools, owner management, filters, and the summary/balance views. Phase 3 connects the app to the Bambu Lab EU catalog so the user can populate the product list by syncing instead of typing, completing the core value.

## Phases

- [ ] **Phase 1: Foundation** - Runnable Windows service with data layer, EF Core migrations, and seeded "Me" owner
- [ ] **Phase 2: Spool & Owner CRUD** - Full spool management, owner management, and summary/balance views in the browser
- [ ] **Phase 3: Bambu Catalog Sync** - Shopify JSON API sync with ImageSharp color extraction and sync-status UI

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
**Plans**: TBD
**UI hint**: no

### Phase 2: Spool & Owner CRUD
**Goal**: Users can add, edit, delete, and filter spools; manage owners; and see totals and per-owner balances — all from the browser.
**Depends on**: Phase 1
**Requirements**: SPOOL-01, SPOOL-02, SPOOL-03, SPOOL-04, SPOOL-05, SPOOL-06, OWNER-01, OWNER-02, BAL-01, BAL-02, BAL-03
**Success Criteria** (what must be TRUE):
  1. User can open the app, add a spool (picking a product from a dropdown with a color swatch preview), assign it to an owner, set weight/price/statuses, and see it appear in the spool list
  2. User can edit a spool's fields and delete a spool; deleting an owner with spools shows an error
  3. User can filter the spool list by owner, material type, spool status, payment status, and free text — combinations work
  4. Summary bar shows correct totals (spools, my spools, total value, total owed); balance table shows one row per non-me owner with spool count, value, and amount owed; rows with price-missing spools are visually flagged
**Plans**: TBD
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

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 0/? | Not started | - |
| 2. Spool & Owner CRUD | 0/? | Not started | - |
| 3. Bambu Catalog Sync | 0/? | Not started | - |
