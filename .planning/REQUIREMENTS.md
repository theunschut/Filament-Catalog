# Requirements — Filament Catalog

## v1 Requirements

### Catalog Sync

- [x] **SYNC-01**: User can trigger a Bambu catalog sync via a "Sync Bambu catalog" button in the UI
- [x] **SYNC-02**: Sync reads product and color variant data from the locally installed Bambu Studio filament profile (`filaments_color_codes.json`) — no web requests required
- [x] **SYNC-03**: Color hex values are read directly from the `fila_color` field in `filaments_color_codes.json`; alpha channel is stripped from 8-digit hex values via `NormalizeHex()`
- [x] **SYNC-04**: Sync upserts into the BambuProduct table, matching on Name + Material to avoid duplicates; updates `LastSyncedAt` on each record
- [x] **SYNC-05**: UI shows when the catalog was last synced; displays a progress indicator while sync is running (202 + polling `/api/sync/status`)
- [x] **SYNC-06**: After the first sync the app works fully offline — BambuProduct table is the source of truth

### Spool Management

- [ ] **SPOOL-01**: User can add a spool by selecting a product from a searchable catalog dropdown (color swatch preview shown on selection)
- [ ] **SPOOL-02**: User can assign a spool to an owner (self or a named friend)
- [ ] **SPOOL-03**: User can set weight (grams), price paid, payment status (paid / unpaid / partial), spool status (sealed / active / empty), and optional notes
- [ ] **SPOOL-04**: User can edit any spool's fields after creation
- [ ] **SPOOL-05**: User can delete a spool
- [ ] **SPOOL-06**: User can filter and search the spool list by owner, material type, spool status, payment status, and free text

### Owner Management

- [ ] **OWNER-01**: User can add a named owner
- [ ] **OWNER-02**: User can delete an owner — rejected with an error if the owner still has spools
- [ ] **OWNER-03**: On first run (empty DB), app seeds one owner with `IsMe = true` and name "Me"

### Summary & Balance

- [ ] **BAL-01**: Summary bar at top of page shows: total spools, my spools, total value, total owed to me
- [ ] **BAL-02**: Balance overview section shows one row per non-me owner: name, spool count, total value, amount owed (sum of unpaid/partial spool prices)
- [ ] **BAL-03**: Balance row visually flags when one or more contributing spools have no price set (so the total looks incomplete)

### Infrastructure

- [ ] **INFRA-01**: App runs as a Windows service (auto-starts with Windows, accessible at `http://localhost:5000`)
- [ ] **INFRA-02**: SQLite database file (`filament.db`) is stored relative to the executable using `AppContext.BaseDirectory` — not a relative working-directory path
- [ ] **INFRA-03**: Single-page UI (`index.html`) served as static files from ASP.NET Core (`UseDefaultFiles` + `UseStaticFiles`)

---

## v2 Requirements (deferred)

- Weight remaining on spool (grams) — status is sufficient for v1
- Partial payment amount tracking (running ledger) — partial treated as unpaid in v1
- Clickable balance rows filtering spool list to that owner
- Multi-region Bambu store support (non-EU)
- Export to CSV

---

## Out of Scope

- Authentication / login — local use only, single user
- Mobile / cross-platform — Windows service is sufficient
- AMS slot tracking — out of scope for inventory management
- QR / NFC labels — unnecessary complexity for v1
- AngleSharp HTML scraping — replaced by local Bambu Studio file
- Shopify JSON API / ImageSharp swatch extraction — replaced by local `filaments_color_codes.json`

---

## Traceability

| REQ-ID  | Phase                    | Status  |
|---------|--------------------------|---------|
| INFRA-01 | Phase 1 — Foundation    | Complete |
| INFRA-02 | Phase 1 — Foundation    | Complete |
| INFRA-03 | Phase 1 — Foundation    | Complete |
| OWNER-03 | Phase 1 — Foundation    | Complete |
| SPOOL-01 | Phase 2 — Spool & Owner CRUD | Complete |
| SPOOL-02 | Phase 2 — Spool & Owner CRUD | Complete |
| SPOOL-03 | Phase 2 — Spool & Owner CRUD | Complete |
| SPOOL-04 | Phase 2 — Spool & Owner CRUD | Complete |
| SPOOL-05 | Phase 2 — Spool & Owner CRUD | Complete |
| SPOOL-06 | Phase 2 — Spool & Owner CRUD | Complete |
| OWNER-01 | Phase 2 — Spool & Owner CRUD | Complete |
| OWNER-02 | Phase 2 — Spool & Owner CRUD | Complete |
| BAL-01  | Phase 2 — Spool & Owner CRUD | Complete |
| BAL-02  | Phase 2 — Spool & Owner CRUD | Complete |
| BAL-03  | Phase 2 — Spool & Owner CRUD | Complete |
| SYNC-01 | Phase 3 — Bambu Catalog Sync | Complete |
| SYNC-02 | Phase 3 — Bambu Catalog Sync | Complete |
| SYNC-03 | Phase 3 — Bambu Catalog Sync | Complete |
| SYNC-04 | Phase 3 — Bambu Catalog Sync | Complete |
| SYNC-05 | Phase 3 — Bambu Catalog Sync | Complete |
| SYNC-06 | Phase 3 — Bambu Catalog Sync | Complete |
