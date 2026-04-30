---
phase: 01-foundation
plan: 03
subsystem: infra
tags: [windows-service, powershell, install-scripts, smoke-test]

requires:
  - 01-01 (csproj, exe build target)
  - 01-02 (Program.cs with AddWindowsService, EF migrations, filament.db path)
provides:
  - install.ps1 — one-command Windows service registration with Automatic startup type
  - uninstall.ps1 — one-command service stop + removal using sc.exe (PS 5.1 compatible)
  - Human-verified: service installs, starts, serves http://localhost:5000, filament.db in publish dir
affects: []

tech-stack:
  added: []
  patterns:
    - "#Requires -RunAsAdministrator guard on both scripts — explicit elevation requirement"
    - "$PSScriptRoot\\publish as default publish dir — never bin/Debug or bin/Release"
    - "sc.exe delete instead of Remove-Service — PowerShell 5.1 compatibility (Windows 11 built-in)"

key-files:
  created:
    - install.ps1
    - uninstall.ps1
  modified: []

key-decisions:
  - "sc.exe delete FilamentCatalog used instead of Remove-Service — Remove-Service requires PS 6+; Windows 11 ships PS 5.1 by default"
  - "install.ps1 uses New-Service (not sc.exe create) — PowerShell native cmdlet is idiomatic and available in PS 5.1"
  - "$PSScriptRoot\\publish as default PublishDir — avoids debug/release build path confusion"

requirements-completed: [INFRA-01]

duration: includes human verification
completed: 2026-04-30
---

# Phase 1 Plan 03: Service Scripts and Human Verification Summary

**Windows service install/uninstall PowerShell scripts with human-verified smoke test — service registers as Automatic startup, serves http://localhost:5000, and filament.db lands in publish dir (not System32)**

## Performance

- **Duration:** includes human verification window
- **Completed:** 2026-04-30
- **Tasks:** 2 (1 auto + 1 human-verify checkpoint)
- **Files modified:** 2

## Accomplishments

- Created install.ps1 at repo root: registers FilamentCatalog Windows service with Automatic startup type, guards against missing exe, defaults publish dir to `$PSScriptRoot\publish`
- Created uninstall.ps1 at repo root: stops service with `-ErrorAction SilentlyContinue` then removes with `sc.exe delete` (PS 5.1 compatible)
- Human checkpoint passed: all 6 verification steps confirmed by user — service installed, started, HTTP 200 at localhost:5000, filament.db confirmed in publish directory, service uninstalled cleanly

## Task Commits

Each task was committed atomically:

1. **Task 1: Write install.ps1 and uninstall.ps1** - `6471c46` (feat)
2. **Fix: sc.exe delete compatibility** - `bb76345` (fix) — applied after human verification

**Plan metadata:** *(committed after SUMMARY)*

## Files Created/Modified

- `install.ps1` - Registers FilamentCatalog Windows service: elevation guard, exe existence check, New-Service with Automatic startup, Start-Service, localhost:5000 message
- `uninstall.ps1` - Stops (SilentlyContinue) and removes service via sc.exe; PS 5.1 compatible

## Decisions Made

- `sc.exe delete` used for service removal — `Remove-Service` is only available in PowerShell 6+; Windows 11 ships with PS 5.1 as the default shell
- `New-Service` retained for install — available in PS 5.1 and idiomatic PowerShell
- Scripts kept separate (not combined) — aligns with plan requirement and single-responsibility principle

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Replaced Remove-Service with sc.exe delete for PS 5.1 compatibility**
- **Found during:** Human verification (post-checkpoint fix)
- **Issue:** The plan's uninstall.ps1 template used `Remove-Service`, which requires PowerShell 6+. Windows 11 ships PowerShell 5.1 as the default. The user encountered this and reported it.
- **Fix:** Replaced `Remove-Service -Name "FilamentCatalog"` with `sc.exe delete FilamentCatalog | Out-Null` and added an explanatory comment. `sc.exe` is available on all Windows versions.
- **Files modified:** `uninstall.ps1`
- **Commit:** `bb76345`

---

**Total deviations:** 1 auto-fixed (Rule 1 - PS 5.1 compatibility)
**Impact on plan:** Uninstall command works correctly on the target OS. No behavior change — service is still removed correctly.

## Human Verification Results

All 6 checkpoint steps passed:

| Step | Check | Result |
|------|-------|--------|
| 1 | dotnet publish succeeded, FilamentCatalog.exe exists in publish/ | PASSED |
| 2 | install.ps1 ran: service installed and started | PASSED |
| 3 | Get-Service shows Status=Running, StartType=Automatic | PASSED |
| 4 | http://localhost:5000/ loads with "Filament Catalog" content | PASSED |
| 5 | filament.db created in publish/ (not C:\Windows\System32) | PASSED |
| 6 | uninstall.ps1 ran: service removed cleanly | PASSED (after sc.exe fix) |

## Issues Encountered

- `Remove-Service` unavailable on PS 5.1 (Windows 11 default) — resolved with `sc.exe delete` fix

## User Setup Required

None — scripts are ready to use after `dotnet publish`. Requires elevated PowerShell session.

## Next Phase Readiness

- Phase 1 foundation complete: scaffold (01-01), Program.cs + migrations (01-02), service scripts (01-03)
- All INFRA requirements satisfied: ASP.NET Core Windows service, EF Core + SQLite, static files, install scripts
- Phase 2 can build the full UI and API on this foundation

## Known Stubs

None — all plan goals achieved with working implementation.

---
*Phase: 01-foundation*
*Completed: 2026-04-30*

## Self-Check: PASSED

| Check | Result |
|-------|--------|
| install.ps1 exists | FOUND |
| uninstall.ps1 exists | FOUND |
| Commit 6471c46 exists | FOUND |
| Commit bb76345 exists | FOUND |
| install.ps1 contains `#Requires -RunAsAdministrator` | PASS |
| install.ps1 contains `New-Service` | PASS |
| install.ps1 contains `StartupType Automatic` | PASS |
| install.ps1 contains `$PSScriptRoot\publish` | PASS |
| uninstall.ps1 contains `sc.exe delete` | PASS |
| uninstall.ps1 contains `-ErrorAction SilentlyContinue` | PASS |
| Human verification: all 6 steps passed | CONFIRMED |
