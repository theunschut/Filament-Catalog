# Milestones — Filament Catalog

---

## v1.0 MVP

**Shipped:** 2026-05-03
**Phases:** 1–6 | **Plans:** 18 | **Timeline:** 4 days (2026-04-30 → 2026-05-03)
**Commits:** 152 | **Files changed:** 137 | **Lines added:** 23,606

### Delivered

A complete local-only filament inventory app running as a Windows service — from bare scaffold to a full-featured UI with Bambu catalog sync, spool CRUD, owner management, balance tracking, and an owner-grouped collapsible tree view with balance sidebar.

### Key Accomplishments

1. **Windows service foundation** — ASP.NET Core 10 service with EF Core + SQLite; install/uninstall PS scripts (sc.exe delete for PS 5.1 compat); human-verified at localhost:5000 with filament.db in publish dir
2. **Full spool & owner CRUD** — 9 API endpoints, live AND-filtered spool list across 5 dimensions, owner management modal, summary bar + balance table; all DOM rendering via textContent (XSS-safe)
3. **Bambu catalog sync** — reads local filaments_color_codes.json (two candidate paths); NormalizeHex strips alpha; upsert on Name+Material; 202 + polling; catalog gate; fully offline after first sync
4. **Layered architecture** — split into FilamentCatalog.EntityFramework (data layer) + FilamentCatalog.Service (web host); [ApiController] MVC controllers with injected service interfaces; Program.cs is pure bootstrapping
5. **Spool duplication** — duplicate button on every row opens Add Spool pre-filled from source spool; catalog selects restored fire-and-forget
6. **Balance sidebar + collapsible tree view** — fixed-width balance sidebar left of spool list; owner-grouped collapsible tree view with localStorage persistence (fc:collapse:{ownerId}); expand/collapse all button; group-aware filter hiding

### Requirements Coverage

21/21 v1 requirements complete (see `.planning/milestones/v1.0-REQUIREMENTS.md`)

### Known Notes at Close

- No milestone audit (`/gsd-audit-milestone`) was run before closing
- Spool catalog data is a denormalized copy at creation time (no FK to BambuProduct) — intentional design decision
- No automated tests; verification via human UAT + SUMMARY.md self-checks

### Archives

- `.planning/milestones/v1.0-ROADMAP.md` — full phase details
- `.planning/milestones/v1.0-REQUIREMENTS.md` — all requirements with outcomes

---
