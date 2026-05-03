# Phase 1: Foundation - Context

**Gathered:** 2026-04-30
**Status:** Ready for planning

<domain>
## Phase Boundary

The app starts as a Windows service, serves a placeholder page at localhost:5000, and has a fully migrated SQLite database with a seeded "Me" owner. Nothing user-visible beyond a browser-accessible page. Phase 2 builds all actual UI on top of this foundation.

Requirements in scope: INFRA-01, INFRA-02, INFRA-03, OWNER-03

</domain>

<decisions>
## Implementation Decisions

### Placeholder page scope
- **D-01:** Claude's discretion — pick the right balance between a minimal "it works" page and a structural HTML skeleton. Either is acceptable; the page will be replaced/extended in Phase 2.

### EF Core schema scope
- **D-02:** Create only the `Owner` table in Phase 1. Each subsequent phase adds its own migration for its own tables. Cleaner separation, easier to review per-phase.

### Logging
- **D-03:** Serilog file logging, writing to a `logs/` directory next to the executable (AppContext.BaseDirectory).
- **D-04:** Log files are separated by date (rolling daily).
- **D-05:** Auto-delete log files older than 7 days.

### Service installation
- **D-06:** Include `install.ps1` and `uninstall.ps1` scripts in the repo root. One-command service registration after publish. Scripts use `sc.exe` or `New-Service` under the hood.

### Claude's Discretion
- Exact placeholder page content/layout
- Serilog sink choice (File vs RollingFile — use whichever supports daily rolling + retention natively)
- PowerShell script style (sc.exe vs New-Service cmdlet)

</decisions>

<specifics>
## Specific Ideas

- Logging: `logs/` subdirectory relative to exe, daily rolling files, 7-day auto-delete
- Service scripts: `install.ps1` / `uninstall.ps1` at repo root, runnable after publish

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Mandatory patterns (from CLAUDE.md)
- `CLAUDE.md` — Critical conventions: SQLite path (`AppContext.BaseDirectory`), static file middleware order (`UseDefaultFiles` before `UseStaticFiles`), EF migrations startup pattern (including stale `__EFMigrationsLock` guard), `DateTime.UtcNow` requirement

### Requirements
- `.planning/REQUIREMENTS.md` §Infrastructure — INFRA-01, INFRA-02, INFRA-03, OWNER-03 are the in-scope requirements

### Project context
- `.planning/PROJECT.md` — Stack constraints, core value, out-of-scope items

No external specs or ADRs beyond CLAUDE.md conventions.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — project has no source code yet. This phase creates the foundation from scratch.

### Established Patterns
- All patterns are defined in CLAUDE.md (see canonical_refs). Downstream agents must follow those conventions; no existing code to infer patterns from.

### Integration Points
- Phase 2 will extend this foundation: add Spool/BambuProduct entities (new migrations), replace/extend the placeholder page with actual UI, wire up API endpoints.

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-foundation*
*Context gathered: 2026-04-30*
