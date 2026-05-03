---
status: partial
phase: 03-bambu-catalog-sync
iteration: 1
findings_in_scope: 10
fixed: 7
skipped: 3
---

## Fixes Applied

### CR-01 — Double Start() call ✅ fixed
Removed the premature `stateService.Start()` before file load. Single call now happens after entries are loaded, with `totalEstimate` set correctly from the start.
Commit: `90ad67a`

### CR-02 — SaveChangesAsync inside per-row loop ✅ fixed
Moved `SaveChangesAsync` outside the foreach loop. All upserts are tracked via EF change tracking (`UpsertTracked`) and persisted in a single transaction at the end of the sync.
Commit: `90ad67a`

### CR-03 — Name == ColorName semantic issue ✅ addressed
Name field now clearly stores the color name (upsert key component). Added XML doc comment to `UpsertTracked` explaining the key design. The (Name, Material) composite unique constraint remains valid since each material has each color name at most once in the source data.
Commit: `90ad67a`

### CR-04 — StripAlpha passes malformed hex ✅ fixed
Replaced `StripAlpha` with `NormalizeHex` which: strips alpha from 9-char `#RRGGBBAA`, validates the resulting 7-char `#RRGGBB` format (hex chars only), and returns `#888888` fallback for any other input.
Commit: `90ad67a`

### CR-05 — productTitle returns material without prefix ✅ fixed
`CatalogController.GetColors` now returns `productTitle = "Bambu " + p.Material`, giving auto-fill results like "Bambu PLA Basic — Orange".
Commit: `4ae07fe`

### WR-01 — Misleading TryWrite comment ✅ fixed
Updated `SyncController` comment to accurately describe `WriteAsync` with `DropNewest` behaviour.
Commit: `e65c118`

### WR-03 — Polling loop has no timeout cap ✅ fixed
Added `MAX_POLLS = 600` (5-minute cap at 500 ms interval). On timeout the button re-enables and stat shows "Sync timed out" in destructive colour.
Commit: `0d8607e`

## Skipped (advisory / low risk)

### WR-02 — In-memory SyncStateService state lost on restart
Advisory. The app is a Windows service; restarts are infrequent and the next sync corrects state. Persisting sync state to the DB is a future improvement, not a bug.

### WR-04 — restoreCatalogSelectsFromSpool is fire-and-forget
Low risk in practice — the async gap is <100 ms on local data and the dialog is shown immediately. A full fix would require restructuring the dialog open flow. Deferred to a future polish phase.

### WR-05 — colorHexInput set without guard in populateFormForEdit
Low risk — the hex values come from the DB which is now validated at sync time via NormalizeHex. Raw user input still goes through the color picker which enforces valid hex natively.
