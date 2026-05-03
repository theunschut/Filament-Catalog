# Retrospective — Filament Catalog

---

## Milestone: v1.0 — MVP

**Shipped:** 2026-05-03
**Phases:** 6 | **Plans:** 18 | **Timeline:** 4 days (2026-04-30 → 2026-05-03)

### What Was Built

1. Windows service foundation with EF Core + SQLite, PS install scripts, human-verified at localhost:5000
2. Full spool & owner CRUD with 9 API endpoints, XSS-safe ES module frontend, live 5-dimension AND filtering
3. Bambu catalog sync from local filaments_color_codes.json; two-step picker; offline-capable after first sync
4. Layered architecture — EF project split, [ApiController] MVC controllers, service interfaces
5. Spool duplication — one-click pre-filled Add dialog
6. Balance sidebar + owner-grouped collapsible tree view with localStorage persistence

### What Worked

- **YOLO mode** suited this project well — solo developer, well-understood domain, fast iteration
- **Phase self-checks** (in SUMMARY.md files) caught issues before human UAT, reducing rework
- **Human UAT at natural checkpoints** (phase ends with UI changes) was the right cadence — not every plan
- **Naming decisions made early** (AppContext.BaseDirectory, UseDefaultFiles before UseStaticFiles) prevented later bugs
- **Denormalized spool copy** was the right call — simpler schema, no FK complexity
- **textContent XSS discipline** enforced from Phase 2 carried cleanly through all phases without backsliding

### What Was Inefficient

- **Phase 4 (refactor) was not planned upfront** — inserted after Phase 3 when the growing endpoint surface made the need obvious; planning it earlier would have saved some rework
- **REQUIREMENTS.md checkbox state** was not kept in sync with the traceability table — created a confusing inconsistency at milestone close
- **No milestone audit** — would have validated cross-phase integration and E2E flows before close; proceeding without it is a known gap

### Patterns Established

- `fc:collapse:{ownerId}` localStorage key scheme for namespaced UI state
- `BackgroundService + Channel<SyncJob>` capacity-1 DropNewest for single-shot background work
- Fire-and-forget catalog select restore for edit/duplicate dialogs (dialog opens immediately, selects populate async)
- Phase SUMMARY.md self-check tables as the standard verification artifact
- Human UAT as a structured 4-item checklist rather than ad-hoc testing

### Key Lessons

- **Check PS version compatibility early** — Remove-Service vs sc.exe was only caught during human verification; scripting notes should flag PS 5.1 as the baseline
- **Architecture refactors are easier if planned as a phase** rather than retrofitted — adding Phase 4 mid-milestone worked but would have been cleaner if anticipated
- **Keep REQUIREMENTS.md checkbox state in sync** with the traceability table after each phase — inconsistency at close creates confusion

### Cost Observations

- Model: Sonnet 4.6 throughout (budget profile)
- Execution time: 4 calendar days, primarily limited by human verification windows
- Notable: Phase 3 (5 plans) and Phase 4 (3 plans) were the most parallel-friendly phases; Phase 6 (2 waves, UI-heavy) benefited most from human UAT at the end

---

## Cross-Milestone Trends

| Metric | v1.0 |
|--------|------|
| Phases | 6 |
| Plans | 18 |
| Days | 4 |
| Commits | 152 |
| Files changed | 137 |
| Lines added | ~23,600 |
| Human UAT issues found | 1 (PS 5.1 Remove-Service) |
| Automated tests | 0 |
