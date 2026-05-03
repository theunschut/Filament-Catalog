# Filament Catalog

## What This Is

A local-only inventory app for tracking Bambu Lab filament spools. Runs as a Windows service (ASP.NET Core 10) at `localhost:5000`. Tracks spools owned by you and friends, with payment status, a balance sidebar showing what each friend owes, and a collapsible owner-grouped spool list. Filament data comes from the locally installed Bambu Studio filament profiles — no internet required after first sync.

## Core Value

Log a new spool quickly by picking from the Bambu catalog — no manual typing of names, materials, or colors.

## Requirements

### Validated

- ✓ App runs as a Windows service (auto-starts with Windows, always at localhost:5000) — v1.0
- ✓ SQLite database at AppContext.BaseDirectory; EF Core migrations on startup — v1.0
- ✓ Single-page UI served as static files (UseDefaultFiles + UseStaticFiles) — v1.0
- ✓ Seeded "Me" owner (IsMe = true) on first run — v1.0
- ✓ User can add/edit/delete spools with owner, weight, price, payment status, spool status, notes — v1.0
- ✓ User can assign spools to owners; owners protected from deletion when they have spools — v1.0
- ✓ User can filter spool list by owner, material, spool status, payment status, and free text — v1.0
- ✓ Summary bar shows total spools, my spools, total value, total owed to me — v1.0
- ✓ Balance sidebar shows per-owner spool count, total value, amount owed; flags unpriced spools — v1.0
- ✓ Spool list grouped by owner in collapsible tree view with localStorage-persisted collapse state — v1.0
- ✓ User can sync Bambu catalog from local filaments_color_codes.json; two-step material/color picker with color swatch preview — v1.0
- ✓ Sync upserts on Name+Material, tracks LastSyncedAt, app works fully offline after first sync — v1.0
- ✓ User can duplicate an existing spool (pre-filled Add dialog) — v1.0
- ✓ Solution split into FilamentCatalog.EntityFramework + FilamentCatalog.Service; [ApiController] MVC controllers with service interfaces — v1.0

### Active

*(Next milestone requirements go here — use /gsd-new-milestone to define them)*

### Out of Scope

- Authentication / login — local use only, single user
- Mobile / cross-platform — Windows service is sufficient
- AMS slot tracking — out of scope for inventory management
- QR / NFC labels — unnecessary complexity
- Weight remaining on spool — status (sealed/active/empty) is sufficient
- Partial payment amount tracking (running ledger) — partial treated as unpaid
- Multi-region Bambu store support — EU store is the target
- Clickable balance rows filtering spool list — nice-to-have, not core workflow

## Context

- Owner: Theun Schut — uses a Bambu Lab P1S printer
- Friends buy rolls via Theun; some pay upfront, some pay later — payment status tracked per spool
- Primary workflow: open app → sync catalog once → log new spool via two-step picker
- Stack: .NET 10, ASP.NET Core with [ApiController] MVC controllers, EF Core + SQLite, plain HTML/CSS/JS (no JS framework, no build step)
- Codebase: ~23,600 lines added across 137 files in v1.0; two-project solution (EntityFramework + Service)

## Constraints

- **Tech stack**: .NET 10, ASP.NET Core [ApiController] MVC — decided; no minimal API
- **Database**: SQLite via EF Core — local file, zero infrastructure; AppContext.BaseDirectory path
- **Deployment**: Windows service via `UseWindowsService()` — auto-starts, accessible at localhost:5000
- **Frontend**: Plain HTML/CSS/JS — no build toolchain, no framework
- **XSS**: All user-supplied strings to DOM via textContent — no innerHTML with untrusted data

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Web app over desktop (WPF/WinForms) | Browser UI easier for data grids, modals, color swatches; hosting as Windows service makes it always-on | ✓ Good — worked well |
| Plain HTML/CSS/JS frontend | No build step, no framework overhead for a local-only tool | ✓ Good — kept iteration fast |
| Local filaments_color_codes.json over Shopify API | Cloudflare blocks .NET HttpClient requests to Bambu webshop; local Bambu Studio install already has the data | ✓ Good — simpler and more reliable |
| [ApiController] MVC over minimal API | Cleaner for growing endpoint surface; Phase 4 migrated from minimal API | ✓ Good — clean separation |
| BackgroundService + Channel\<SyncJob\> capacity 1 DropNewest | Simplest correct pattern for single-shot background work | ✓ Good — no issues |
| 202 + polling over SSE | Simpler to implement and debug in plain HTML/JS | ✓ Good — straightforward |
| sc.exe delete for PS 5.1 compat | Remove-Service requires PS 6+; Windows 11 ships PS 5.1 by default | ✓ Good — discovered during human UAT |
| Denormalized spool copy (no FK to BambuProduct) | Catalog can change; spool data should be stable at creation time | ✓ Good — intentional |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-05-03 after v1.0 milestone*
