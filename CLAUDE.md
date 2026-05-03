# Filament Catalog — Project Guide

## What This Is

A local-only ASP.NET Core 10 web app for tracking Bambu Lab filament spools. Runs as a Windows service at `http://localhost:5000`. Tracks your own spools and friends' spools with payment status and balance tracking. Filament catalog populated from the local Bambu Studio installation.

## Stack

- **Backend**: .NET 10, ASP.NET Core minimal API, Windows service (`AddWindowsService()`)
- **Database**: EF Core 10 + SQLite — `filament.db` stored at `AppContext.BaseDirectory`
- **Catalog source**: Local `filaments_color_codes.json` from Bambu Studio installation — two candidate paths tried (AppData first, Program Files fallback); no web requests
- **Color extraction**: Color hex read directly from `fila_color` field in JSON; `NormalizeHex()` strips alpha from 9-char `#RRGGBBAA` and validates `#RRGGBB` format
- **Frontend**: Plain HTML/CSS/JS, ES modules, native `<dialog>` for modals — no build step, no framework

## Critical Conventions

- **SQLite path**: Always use `Path.Combine(AppContext.BaseDirectory, "filament.db")` — never a relative path. The Windows service working directory is `C:\Windows\System32`.
- **Static files**: `app.UseDefaultFiles()` MUST come before `app.UseStaticFiles()` — order is mandatory.
- **Background sync**: `BackgroundService` + `Channel<SyncJob>` (capacity 1, DropNewest). Sync state exposed via `SyncStateService` singleton.
- **Sync progress**: 202 + polling `/api/sync/status` — not SSE.
- **Migrations**: `MigrateAsync()` on startup. Add a startup guard to clear stale `__EFMigrationsLock` rows.
- **DateTime**: Always `DateTime.UtcNow` — avoids EF Core 10 SQLite timezone breaking changes.
- **Catalog sync**: `SyncService` reads `filaments_color_codes.json` via `IServiceScopeFactory` (scoped from singleton `SyncBackgroundService`). `UpsertTracked()` uses EF change tracking; single `SaveChangesAsync` after the loop.

## GSD Workflow

This project uses [GSD](https://github.com/get-shit-done/gsd) for structured planning.

- Planning docs: `.planning/`
- Roadmap: `.planning/ROADMAP.md`
- Current phase: see `.planning/STATE.md`
- Mode: YOLO (auto-approve)

**Next step**: `/gsd-discuss-phase 1` or `/gsd-plan-phase 1`
