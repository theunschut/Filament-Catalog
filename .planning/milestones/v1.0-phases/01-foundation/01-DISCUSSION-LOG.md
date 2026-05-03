# Phase 1: Foundation — Discussion Log

**Date:** 2026-04-30
**Areas discussed:** Placeholder page scope, EF Core schema scope, Logging setup, Service install method

---

## Area: Placeholder page scope

**Question:** What should the Phase 1 placeholder page contain?

| Option | Description |
|--------|-------------|
| HTML skeleton | Basic layout with nav/header, Phase 2 fills content |
| Minimal 'it works' | Just a heading and status message |
| **You decide** | Claude picks the right balance |

**User selected:** You decide

**Notes:** User is comfortable with Claude picking the appropriate level of structure.

---

## Area: EF Core schema scope

**Question:** Create only the Owner table, or scaffold all tables upfront?

| Option | Description |
|--------|-------------|
| **Owner table only** ✓ | Only what Phase 1 uses; each phase adds its own migration |
| All tables now | Create Owner + Spool + BambuProduct in one migration |
| You decide | Claude picks |

**User selected:** Owner table only (Recommended)

---

## Area: Logging setup

**Question:** How should the app log?

| Option | Description |
|--------|-------------|
| **File logging** ✓ | Serilog, logs next to exe |
| Windows Event Log | Native for services, harder to access |
| Console only | Invisible when running as service |
| You decide | Claude picks |

**User selected:** File logging with Serilog

**User clarification:** `logs/` directory next to exe, separated by date, auto-delete if older than 7 days.

---

## Area: Service install method

**Question:** How should the Windows service be installed?

| Option | Description |
|--------|-------------|
| **PowerShell script** ✓ | install.ps1 / uninstall.ps1 in repo root |
| README instructions | Document sc.exe commands only |
| You decide | Claude picks |

**User selected:** PowerShell script (Recommended)

---

## Summary

4 areas discussed. All decisions captured. No deferred ideas. Ready for planning.
