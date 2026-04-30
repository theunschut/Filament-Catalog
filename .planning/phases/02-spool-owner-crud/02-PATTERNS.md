# Phase 2: Spool & Owner CRUD - Pattern Map

**Mapped:** 2026-05-01
**Files analyzed:** 11 new/modified files
**Analogs found:** 6 / 11 (5 frontend files have no existing analog — project is greenfield for JS)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `FilamentCatalog/Models/Spool.cs` | model | CRUD | `FilamentCatalog/Models/Owner.cs` | role-match |
| `FilamentCatalog/Models/PaymentStatus.cs` | model | — | `FilamentCatalog/Models/Owner.cs` | partial (no enum exists yet) |
| `FilamentCatalog/Models/SpoolStatus.cs` | model | — | `FilamentCatalog/Models/Owner.cs` | partial (no enum exists yet) |
| `FilamentCatalog/AppDbContext.cs` | config | CRUD | `FilamentCatalog/AppDbContext.cs` (self — extend) | exact |
| `FilamentCatalog/Program.cs` | config/route | request-response | `FilamentCatalog/Program.cs` (self — extend) | exact |
| `FilamentCatalog/Migrations/*_AddSpools.*` | migration | CRUD | `FilamentCatalog/Migrations/20260430215538_InitialCreate.cs` | role-match |
| `wwwroot/index.html` | component | request-response | `wwwroot/index.html` (self — replace placeholder) | partial |
| `wwwroot/css/app.css` | config | — | none | no analog |
| `wwwroot/js/app.js` | utility | request-response | none | no analog |
| `wwwroot/js/api.js` | service | request-response | none | no analog |
| `wwwroot/js/spools.js` | component | request-response | none | no analog |
| `wwwroot/js/owners.js` | component | request-response | none | no analog |
| `wwwroot/js/summary.js` | component | request-response | none | no analog |

---

## Pattern Assignments

### `FilamentCatalog/Models/Spool.cs` (model, CRUD)

**Analog:** `FilamentCatalog/Models/Owner.cs`

**Entity structure pattern** (Owner.cs lines 1-7 — the complete file):
```csharp
public class Owner
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsMe { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Apply this way for Spool.cs:**
- No namespace declaration (file-scoped implicit namespace — matches Owner.cs which has none)
- No `using` directives at the entity level (Owner.cs has none)
- `public int Id { get; set; }` — auto-increment PK, same as Owner
- `required string` on non-nullable string fields (same as `required string Name` in Owner)
- `DateTime CreatedAt { get; set; }` — no default, set explicitly to `DateTime.UtcNow` at insert time (same as Owner seeding in Program.cs line 71)
- Navigation property for FK: `public Owner Owner { get; set; } = null!;` — null-forgiving assignment for required nav property (EF Core convention)
- Explicit FK property `public int OwnerId { get; set; }` alongside nav property (avoids EF shadow property — see RESEARCH.md Pitfall 2)

**Enum fields pattern** — no existing analog; declare as plain C# enums in separate files, use EF Core integer storage (default), configure `JsonStringEnumConverter` globally in Program.cs:
```csharp
public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
public SpoolStatus SpoolStatus { get; set; } = SpoolStatus.Sealed;
```

---

### `FilamentCatalog/Models/PaymentStatus.cs` and `FilamentCatalog/Models/SpoolStatus.cs` (model, —)

**No analog exists** — no enums in the codebase yet.

**Pattern from RESEARCH.md Code Examples:**
```csharp
// Models/PaymentStatus.cs
public enum PaymentStatus { Paid, Unpaid, Partial }

// Models/SpoolStatus.cs
public enum SpoolStatus { Sealed, Active, Empty }
```

- One enum per file (matches Owner.cs one-class-per-file convention)
- No namespace, no using directives (matches Owner.cs style)

---

### `FilamentCatalog/AppDbContext.cs` (config, CRUD)

**Analog:** `FilamentCatalog/AppDbContext.cs` itself (extend in place)

**Full current file** (AppDbContext.cs lines 1-8 — complete file):
```csharp
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Owner> Owners => Set<Owner>();
}
```

**Apply this way — add two things:**

1. Add `DbSet<Spool>` using same expression-body property pattern:
```csharp
public DbSet<Spool> Spools => Set<Spool>();
```

2. Add `OnModelCreating` override to configure the FK with `DeleteBehavior.Restrict` (D-03):
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Spool>()
        .HasOne(s => s.Owner)
        .WithMany()
        .HasForeignKey(s => s.OwnerId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

**Critical:** `DeleteBehavior.Restrict` is not EF Core's default — must be explicit. Default for required FK is `Cascade`; default for optional FK is `ClientSetNull`. Neither is acceptable per D-03.

---

### `FilamentCatalog/Program.cs` (config/route, request-response)

**Analog:** `FilamentCatalog/Program.cs` itself (extend in place)

**Full current file** (Program.cs lines 1-74 — complete file):

- Lines 1-3: `using` directives for Serilog, EF Core
- Lines 5-14: Bootstrap Serilog logger to file at `AppContext.BaseDirectory/logs/`
- Lines 16-44: `try` block — WebApplication builder, services, app pipeline, `RunAsync()`
- Lines 22-24: `AddWindowsService()` — do not remove
- Lines 27-29: SQLite path using `Path.Combine(AppContext.BaseDirectory, "filament.db")` — do not change
- Lines 33-39: Startup scope — `ClearStaleEfMigrationsLock` + `MigrateAsync` + `SeedAsync` — keep exactly
- Lines 41-42: `UseDefaultFiles()` BEFORE `UseStaticFiles()` — mandatory order per CLAUDE.md
- Line 44: `await app.RunAsync()` — all `Map*` calls must appear before this line

**New `using` to add at top** (for JSON enum serialization):
```csharp
using System.Text.Json.Serialization;
```

**New service registration to add** (before `var app = builder.Build()`):
```csharp
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

**Endpoint registration insertion point** — between line 42 (`app.UseStaticFiles()`) and line 44 (`await app.RunAsync()`):
```csharp
// --- Owners ---
app.MapGet("/api/owners", async (AppDbContext db) =>
    await db.Owners.OrderBy(o => o.CreatedAt).ToListAsync());

app.MapPost("/api/owners", async (AppDbContext db, OwnerCreateRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.UnprocessableEntity(new { error = "Name is required." });
    var owner = new Owner { Name = req.Name.Trim(), IsMe = false, CreatedAt = DateTime.UtcNow };
    db.Owners.Add(owner);
    await db.SaveChangesAsync();
    return Results.Created($"/api/owners/{owner.Id}", owner);
});

app.MapDelete("/api/owners/{id:int}", async (AppDbContext db, int id) =>
{
    var owner = await db.Owners.FindAsync(id);
    if (owner is null) return Results.NotFound(new { error = "Owner not found." });
    if (owner.IsMe) return Results.UnprocessableEntity(new { error = "Cannot delete the 'Me' owner." });
    var spoolCount = await db.Spools.CountAsync(s => s.OwnerId == id);
    if (spoolCount > 0)
        return Results.Conflict(new { error = $"Cannot delete — {spoolCount} spool(s) assigned. Remove spools first." });
    db.Owners.Remove(owner);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// --- Spools ---
app.MapGet("/api/spools", async (AppDbContext db) =>
    await db.Spools.Include(s => s.Owner).OrderBy(s => s.CreatedAt).ToListAsync());

// POST, PUT, DELETE follow same pattern — see Core Pattern below
```

**Core endpoint pattern** (every handler follows this shape):
```csharp
app.Map[Verb]("/api/route/{id:int}", async (AppDbContext db, int id, RequestDto? body) =>
{
    // 1. Fetch entity — 404 if not found
    var entity = await db.Entity.FindAsync(id);
    if (entity is null) return Results.NotFound(new { error = "Not found." });

    // 2. Validate input — 422 if invalid
    if (string.IsNullOrWhiteSpace(body?.Name))
        return Results.UnprocessableEntity(new { error = "Name is required." });

    // 3. Business rule checks — 409 if constraint violated
    // (e.g., owner-has-spools check)

    // 4. Mutate + save
    entity.Name = body.Name.Trim();
    await db.SaveChangesAsync();

    // 5. Return appropriate result
    return Results.Ok(entity);          // 200 for PUT
    return Results.Created(url, entity); // 201 for POST
    return Results.NoContent();          // 204 for DELETE
});
```

**Error response shape** — always `new { error = "..." }` (D-09):
```csharp
return Results.NotFound(new { error = "Spool not found." });
return Results.Conflict(new { error = "Cannot delete — ..." });
return Results.UnprocessableEntity(new { error = "Name is required." });
```

**SeedAsync pattern** (Program.cs lines 67-74) — existing pattern, do not change:
```csharp
static async Task SeedAsync(AppDbContext db)
{
    if (!await db.Owners.AnyAsync())
    {
        db.Owners.Add(new Owner { Name = "Me", IsMe = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}
```
Note `DateTime.UtcNow` — mandatory per CLAUDE.md. Spool.CreatedAt must follow same pattern.

---

### `FilamentCatalog/Migrations/*_AddSpools.*` (migration, CRUD)

**Analog:** `FilamentCatalog/Migrations/20260430215538_InitialCreate.cs`

**Do not hand-write this file.** Generate with:
```
dotnet ef migrations add AddSpools
```
Run from the `FilamentCatalog/` project directory.

**Migration structure pattern** (InitialCreate.cs lines 1-37 — complete file):
```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FilamentCatalog.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Owners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsMe = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owners", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Owners");
        }
    }
}
```

**Expected generated output for AddSpools** will include:
- `CreateTable("Spools", ...)` with all Spool columns
- A `ForeignKey` constraint referencing `PK_Owners` — EF Core generates this from the `HasForeignKey` + `OnDelete(DeleteBehavior.Restrict)` configuration
- Enum columns (`PaymentStatus`, `SpoolStatus`) stored as `INTEGER` (EF Core default for enums in SQLite)
- `Down()` method: `DropTable("Spools")`

**Verify after generation:** confirm the FK uses `onDelete: ReferentialAction.Restrict` in the generated file.

---

### `wwwroot/index.html` (component, request-response)

**Analog:** `wwwroot/index.html` itself (replace placeholder)

**Current placeholder** (index.html lines 1-22 — complete file):
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Filament Catalog</title>
  <style>
    /* Phase 1 placeholder styles — extend in Phase 2 */
    body { font-family: system-ui, sans-serif; margin: 0; padding: 2rem; background: #f5f5f5; }
    h1 { color: #333; }
    .status { color: #666; font-size: 0.9rem; }
  </style>
</head>
<body>
  <h1>Filament Catalog</h1>
  <!-- Phase 2: replace this placeholder with actual UI -->
  <main>
    <p class="status">Service is running.</p>
  </style>
  <!-- Phase 2: add ES module scripts here -->
</body>
</html>
```

**Replace with full page following this structure:**
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Filament Catalog</title>
  <link rel="stylesheet" href="/css/app.css" />
</head>
<body>
  <!-- Sticky summary bar -->
  <header id="summary-bar">...</header>

  <!-- Filter bar -->
  <div id="filter-bar">...</div>

  <!-- Spool list -->
  <main id="spool-list">...</main>

  <!-- Balance section -->
  <details open id="balance-section">
    <summary>Balance Overview</summary>
    <table id="balance-table">...</table>
  </details>

  <!-- Spool add/edit dialog -->
  <dialog id="spool-dialog">...</dialog>

  <!-- Owner management dialog -->
  <dialog id="owner-dialog">...</dialog>

  <script type="module" src="/js/app.js"></script>
</body>
</html>
```

**Key structural rules from UI-SPEC:**
- `<details open>` for balance section — no JS toggle (D-05)
- `<dialog>` (native, not div overlay) for both modals (CLAUDE.md)
- `<script type="module">` — ES modules, no bundler (CLAUDE.md)
- Single `<link rel="stylesheet">` — no inline `<style>` block in Phase 2 version
- Gear icon: Unicode `⚙` or inline SVG, aria-label="Manage owners"

---

### `wwwroot/css/app.css` (config, —)

**No analog exists** — first CSS file in the project.

**CSS custom properties root** (from UI-SPEC and RESEARCH.md — use as opening block):
```css
:root {
    --color-bg: #f5f5f5;
    --color-surface: #ffffff;
    --color-accent: #2563eb;
    --color-destructive: #dc2626;
    --color-border: #e2e8f0;
    --color-muted: #6b7280;
    --color-warning: #f59e0b;
    --font-body: system-ui, sans-serif;
    --space-xs: 4px;
    --space-sm: 8px;
    --space-md: 16px;
    --space-lg: 24px;
    --space-xl: 32px;
}
```

**Summary bar structure** (from UI-SPEC):
```css
#summary-bar {
    position: sticky;
    top: 0;
    background: #ffffff;
    border-bottom: 1px solid #e2e8f0;
    height: 48px;
    padding: 0 16px;
    display: flex;
    align-items: center;
    gap: 32px;
    z-index: 100;
}
```

**Filter bar structure** (from UI-SPEC):
```css
#filter-bar {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    padding: 8px 16px;
    background: #ffffff;
    border-bottom: 1px solid #e2e8f0;
}
```

**Chip button states** (from UI-SPEC):
```css
.chip { height: 32px; padding: 0 16px; border-radius: 16px; font-size: 14px; font-weight: 600; cursor: pointer; }
.chip.active { background: #2563eb; color: #ffffff; border: none; }
.chip:not(.active) { background: #ffffff; border: 1px solid #e2e8f0; color: #374151; }
```

**Badge colors** (from UI-SPEC — spool list read-only status badges):
```css
.badge-sealed   { background: #dbeafe; color: #1e40af; }
.badge-active   { background: #dcfce7; color: #166534; }
.badge-empty    { background: #f3f4f6; color: #6b7280; }
.badge-paid     { background: #dcfce7; color: #166534; }
.badge-unpaid   { background: #fee2e2; color: #991b1b; }
.badge-partial  { background: #fef3c7; color: #92400e; }
```

**Error banner** (from UI-SPEC):
```css
.error-banner {
    background: #fee2e2;
    border: 1px solid #fca5a5;
    color: #991b1b;
    padding: 8px 16px;
    border-radius: 6px;
    font-size: 14px;
}
```

---

### `wwwroot/js/app.js` (utility, request-response)

**No analog exists** — first JS file in the project.

**Entry point pattern** (from RESEARCH.md Pattern 4):
```javascript
// wwwroot/js/app.js
import { getSpools, getOwners, getSummary, getBalance } from './api.js';
import { renderSpools, applyFilters } from './spools.js';
import { initOwnerModal } from './owners.js';
import { renderSummary, renderBalance } from './summary.js';

// Page-load init
const [spools, owners, summary, balance] = await Promise.all([
    getSpools(), getOwners(), getSummary(), getBalance()
]);

renderSummary(summary);
renderBalance(balance);
renderSpools(spools, owners);
initOwnerModal();
```

**Custom event listener pattern** (from RESEARCH.md Pattern 5):
```javascript
document.addEventListener('owners-updated', async () => {
    const owners = await getOwners();
    // repopulate owner select in filter bar and spool form
});
```

---

### `wwwroot/js/api.js` (service, request-response)

**No analog exists** — first JS file in the project.

**Fetch wrapper pattern** (from RESEARCH.md Pattern 4):
```javascript
// wwwroot/js/api.js
export async function getSpools() {
    const res = await fetch('/api/spools');
    if (!res.ok) throw new Error(await res.text());
    return res.json();
}

export async function createSpool(data) {
    const res = await fetch('/api/spools', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
    if (!res.ok) throw new Error((await res.json()).error ?? 'Server error');
    return res.json();
}
```

**All 10 wrappers follow this same shape:**
- GET endpoints: `fetch(url)`, throw if `!res.ok`, return `res.json()`
- POST/PUT: include `method`, `headers: { 'Content-Type': 'application/json' }`, `body: JSON.stringify(data)`
- DELETE: `method: 'DELETE'`, no body, check `!res.ok`, no return value (204 response)
- Error extraction: `(await res.json()).error ?? 'Server error'` for mutation errors

---

### `wwwroot/js/spools.js` (component, request-response)

**No analog exists** — first JS file in the project.

**Filter function pattern** (from RESEARCH.md Code Examples):
```javascript
// wwwroot/js/spools.js
function applyFilters(allSpools, { ownerFilter, materialFilter, spoolStatusFilter, paymentStatusFilter, searchText }) {
    return allSpools.filter(spool => {
        if (ownerFilter && spool.ownerId !== parseInt(ownerFilter)) return false;
        if (materialFilter && spool.material !== materialFilter) return false;
        if (spoolStatusFilter.size > 0 && !spoolStatusFilter.has(spool.spoolStatus)) return false;
        if (paymentStatusFilter.size > 0 && !paymentStatusFilter.has(spool.paymentStatus)) return false;
        if (searchText) {
            const q = searchText.toLowerCase();
            const hay = `${spool.name} ${spool.material} ${spool.notes ?? ''}`.toLowerCase();
            if (!hay.includes(q)) return false;
        }
        return true;
    });
}
```

**Key filter rules** (from RESEARCH.md Pitfall 6):
- `spoolStatusFilter` and `paymentStatusFilter` are `Set<string>` — empty Set = "show all"
- AND logic across all five filter controls
- Never re-fetch from API on filter change — toggle `hidden` attribute on rows

**Color hex input sync pattern** (from RESEARCH.md Code Examples):
```javascript
const HEX_RE = /^#[0-9A-Fa-f]{6}$/;

colorPicker.addEventListener('input', () => {
    hexText.value = colorPicker.value;
    swatch.style.background = colorPicker.value;
});

hexText.addEventListener('input', () => {
    if (HEX_RE.test(hexText.value)) {
        colorPicker.value = hexText.value;
        swatch.style.background = hexText.value;
    }
});

function getColorHex() {
    return HEX_RE.test(hexText.value) ? hexText.value : '#888888';
}
```

---

### `wwwroot/js/owners.js` (component, request-response)

**No analog exists** — first JS file in the project.

**Native dialog pattern** (from RESEARCH.md Pattern 5):
```javascript
// Open on gear icon click
gearBtn.addEventListener('click', async () => {
    const owners = await getOwners();
    renderOwnerList(owners);
    dialog.showModal();
});

// Backdrop click closes
dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });

// Dispatch owners-updated on close so filter bar and spool form refresh
dialog.addEventListener('close', () => {
    document.dispatchEvent(new CustomEvent('owners-updated'));
});
```

**Inline error display pattern** (from UI-SPEC):
```javascript
// On 409 from DELETE /api/owners/{id}
function showOwnerError(rowEl, message) {
    let err = rowEl.querySelector('.error-banner');
    if (!err) { err = document.createElement('p'); err.className = 'error-banner'; rowEl.after(err); }
    err.textContent = message;
}
// Clear error before next attempt
function clearOwnerErrors() {
    document.querySelectorAll('#owner-dialog .error-banner').forEach(el => el.remove());
}
```

---

### `wwwroot/js/summary.js` (component, request-response)

**No analog exists** — first JS file in the project.

**Summary bar render pattern** (from D-11, UI-SPEC):
```javascript
export function renderSummary({ totalSpools, mySpools, totalValue, totalOwed }) {
    document.getElementById('stat-total-spools').textContent = totalSpools;
    document.getElementById('stat-my-spools').textContent = mySpools;
    document.getElementById('stat-total-value').textContent = formatCurrency(totalValue);
    document.getElementById('stat-total-owed').textContent = formatCurrency(totalOwed);
}

function formatCurrency(value) {
    return new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(value);
}
```

**Balance table render pattern** (from D-10, BAL-03, UI-SPEC):
```javascript
export function renderBalance(rows) {
    const tbody = document.querySelector('#balance-table tbody');
    if (rows.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" style="color:#6b7280;font-style:italic">No other owners yet.</td></tr>';
        return;
    }
    tbody.innerHTML = rows.map(row => `
        <tr>
            <td>${row.ownerName}${row.hasUnpriced
                ? ' <span style="color:#f59e0b" title="One or more spools have no price — totals may be incomplete.">⚠</span>'
                : ''}</td>
            <td>${row.spoolCount}</td>
            <td>${formatCurrency(row.value)}</td>
            <td>${formatCurrency(row.owed)}</td>
        </tr>
    `).join('');
}
```

---

## Shared Patterns

### DateTime — Always UTC
**Source:** `FilamentCatalog/Program.cs` line 71
**Apply to:** All endpoints that create entities (`POST /api/owners`, `POST /api/spools`)
```csharp
CreatedAt = DateTime.UtcNow
```
Never use `DateTime.Now`. Mandatory per CLAUDE.md.

### Structured JSON Error Response
**Source:** RESEARCH.md Pattern 1 (D-09 decision)
**Apply to:** All API endpoints that can return errors
```csharp
return Results.NotFound(new { error = "Spool not found." });
return Results.Conflict(new { error = "Cannot delete — {N} spool(s) assigned. Remove spools first." });
return Results.UnprocessableEntity(new { error = "Name is required." });
```
Shape is always `{ "error": "..." }` — never a plain string, never a different key name.

### ColorHex Default
**Source:** D-02 decision
**Apply to:** `POST /api/spools` and `PUT /api/spools/{id}` handlers
```csharp
var colorHex = string.IsNullOrWhiteSpace(req.ColorHex) ? "#888888" : req.ColorHex;
```
Applied server-side before saving — browser pre-validation is optional.

### ES Module Import Convention
**Source:** CLAUDE.md + index.html placeholder comment
**Apply to:** All `.js` files in `wwwroot/js/`
```javascript
// Named exports from each module
export function renderSpools(...) { ... }
export function applyFilters(...) { ... }

// Single entry point imports all
import { renderSpools } from './spools.js';
```
Relative paths with `.js` extension. No bare specifiers. No bundler.

### Enum Serialization as Strings
**Source:** RESEARCH.md Pitfall 1
**Apply to:** `FilamentCatalog/Program.cs` service registration block (once, global)
```csharp
using System.Text.Json.Serialization;

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```
Without this, `PaymentStatus` and `SpoolStatus` serialize as integers (`0`, `1`, `2`) in JSON responses. The JS filter (`spoolStatusFilter.has(spool.spoolStatus)`) depends on string values like `"Sealed"`, `"Active"`.

### Startup Scope Pattern
**Source:** `FilamentCatalog/Program.cs` lines 33-39
**Apply to:** No new startup work in Phase 2 — existing pattern must be preserved unchanged
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ClearStaleEfMigrationsLock(db);
    await db.Database.MigrateAsync();
    await SeedAsync(db);
}
```
The new `AddSpools` migration is picked up automatically by `MigrateAsync()` — no code change needed here.

---

## No Analog Found

Files with no close match in the codebase (use RESEARCH.md patterns and UI-SPEC as primary reference):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `wwwroot/css/app.css` | config | — | First CSS file; no existing stylesheets |
| `wwwroot/js/app.js` | utility | request-response | First JS file; no existing scripts |
| `wwwroot/js/api.js` | service | request-response | First JS file; no existing fetch layer |
| `wwwroot/js/spools.js` | component | request-response | First JS file; no existing DOM rendering |
| `wwwroot/js/owners.js` | component | request-response | First JS file; no existing modal logic |
| `wwwroot/js/summary.js` | component | request-response | First JS file; no existing aggregation rendering |

For these files, RESEARCH.md Code Examples and UI-SPEC Component Inventory sections are the authoritative pattern sources.

---

## Anti-Patterns Confirmed from Codebase

These are violations to actively avoid, grounded in the existing code:

| Anti-Pattern | Why Wrong | What to Do Instead |
|---|---|---|
| `DateTime.Now` | CLAUDE.md + Program.cs line 71 shows `DateTime.UtcNow` as the established pattern | Always `DateTime.UtcNow` |
| Relative SQLite path | Program.cs line 27: `Path.Combine(AppContext.BaseDirectory, "filament.db")` is the established path | Do not introduce any new DB path construction |
| `UseStaticFiles()` before `UseDefaultFiles()` | Program.cs lines 41-42 show correct order with an explicit comment | `UseDefaultFiles()` first, always |
| `Map*` calls after `await app.RunAsync()` | Program.cs line 44: `RunAsync()` blocks — routes registered after it are never reached | All `Map*` before line 44 |
| Default cascade delete on Spool→Owner FK | EF Core default is Cascade for required FKs — violates D-03 | Explicit `OnDelete(DeleteBehavior.Restrict)` in `OnModelCreating` |
| Enums serializing as integers | Without `JsonStringEnumConverter`, JS filter by string enum value breaks | Register converter in `ConfigureHttpJsonOptions` |

---

## Metadata

**Analog search scope:** `FilamentCatalog/` (all .cs files), `FilamentCatalog/wwwroot/` (all static files), `FilamentCatalog/Migrations/` (all migration files)
**Files scanned:** 7 source files read in full
**Pattern extraction date:** 2026-05-01
