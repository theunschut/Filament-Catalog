# Filament Catalog

## What This Is

A local-only inventory app for tracking Bambu Lab filament spools. Runs as a Windows service (ASP.NET Core) and is accessed via browser at `localhost:5000`. Tracks your own spools and spools owned by friends, with payment status and a balance view showing what friends owe you. Filament data comes from a scraped Bambu Lab EU store catalog.

## Core Value

Log a new spool quickly by picking from the Bambu catalog — no manual typing of names, materials, or colors.

## Requirements

### Validated

- [x] App runs as a Windows service (auto-starts with Windows, always at localhost:5000) — *Validated in Phase 1: Foundation*
- [x] Solution is split into two projects (FilamentCatalog.EntityFramework data layer + FilamentCatalog.Service web host) with clean architecture boundaries — *Validated in Phase 4: Refactor Project Structure*
- [x] API endpoints organized into [ApiController] controllers with constructor-injected service interfaces; Program.cs is pure bootstrapping — *Validated in Phase 4: Refactor Project Structure*

### Active

- [ ] User can sync the Bambu Lab EU filament catalog (scrape products + extract dominant color from swatch images)
- [ ] User can add a spool by selecting a product from the synced catalog (color preview shown)
- [ ] User can assign a spool to an owner (self or a named friend)
- [ ] User can set and update payment status per spool (paid / unpaid / partial)
- [ ] User can set and update spool status (sealed / active / empty)
- [ ] User can filter and search the spool list by owner, status, payment, and free text
- [ ] User can edit or delete any spool
- [ ] User can manage owners (add / delete; delete rejected if owner has spools)
- [ ] User sees a summary bar (total spools, my spools, total value, total owed to me)
- [ ] User sees a balance overview per non-me owner (spool count, total value, amount owed)

### Out of Scope

- Authentication / login — local use only, no auth needed
- Partial payment amount tracking — partial status treated as unpaid for debt calculations
- Weight remaining on spool — status (sealed/active/empty) is sufficient for v1
- Mobile / cross-platform — Windows-only is fine
- Multiple Bambu store regions — EU store is the target

## Context

- Owner: Theun Schut — uses a Bambu Lab P1S printer
- Friends buy rolls via Theun; some pay upfront, some pay later — payment status tracked per spool
- Primary workflow: open app → log new spool → pick from catalog dropdown
- After first catalog sync, app works fully offline
- Stack: .NET 10, ASP.NET Core minimal API, EF Core + SQLite, AngleSharp (scraping), ImageSharp (color extraction), plain HTML/CSS/JS (no JS framework)

## Constraints

- **Tech stack**: .NET 10, ASP.NET Core with [ApiController] MVC controllers — decided; Phase 4 migrated from minimal API to controller-based architecture
- **Database**: SQLite via EF Core — local file, zero infrastructure
- **Deployment**: Windows service via `UseWindowsService()` — auto-starts, accessible at localhost:5000
- **Frontend**: Plain HTML/CSS/JS — no build toolchain, no framework

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Web app over desktop (WPF/WinForms) | Browser UI is easier to build for data grids, modals, color swatches; hosting as Windows service makes it always-on | — Pending |
| Plain HTML/CSS/JS frontend | No build step, no framework overhead for a local-only tool | — Pending |
| AngleSharp for scraping | .NET-native HTML parser, no external process needed | — Pending |
| ImageSharp for color extraction | Pure .NET, handles swatch image download + dominant color extraction in-process | — Pending |

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

- [ ] User can duplicate an existing spool — opens Add Spool dialog pre-filled from source spool — *Validated in Phase 5: Spool Duplication*

---
*Last updated: 2026-05-02 after Phase 5 completion*
