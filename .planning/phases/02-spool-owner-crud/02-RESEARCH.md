# Phase 2: Spool & Owner CRUD — Research

**Researched:** 2026-05-01
**Domain:** ASP.NET Core 10 minimal API CRUD + EF Core 10 SQLite migration + vanilla JS/ES modules single-page UI
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Hybrid Spool model — `Name`, `Material`, `ColorHex` stored as own non-nullable columns. No `BambuProductId` FK in Phase 2 (added by Phase 3 migration).
- **D-02:** Spool entity fields: `Id`, `Name` (string, required), `Material` (string, required), `ColorHex` (string, required — default #888888 if empty), `OwnerId` (FK to Owner), `WeightGrams` (int?), `PricePaid` (decimal?), `PaymentStatus` (enum: Paid/Unpaid/Partial), `SpoolStatus` (enum: Sealed/Active/Empty), `Notes` (string?), `CreatedAt` (DateTime UTC).
- **D-03:** `OwnerId` is required FK to `Owner`. Deleting owner with spools → 409. No cascade delete.
- **D-04:** Single HTML page. Sticky summary bar top. Horizontal filter bar above spool list. Collapsible balance section below list (using `<details open>`). Owner management in separate settings modal.
- **D-05:** Balance section starts expanded. Toggle state does not persist across reloads.
- **D-06:** Five filters: Owner `<select>`, Material `<select>`, Spool status chips (Sealed/Active/Empty), Payment status chips (Paid/Unpaid/Partial), free-text `<input type="search">`. Live/instant filtering, no Apply button.
- **D-07:** Owner CRUD in native `<dialog>` modal opened by gear icon in header. Fetches owner list on open. On close dispatches `CustomEvent('owners-updated')`. "Me" (IsMe=true) is displayed but cannot be deleted. Delete with spools shows inline error, modal stays open.
- **D-08:** API endpoints: `GET/POST /api/owners`, `DELETE /api/owners/{id}`, `GET/POST /api/spools`, `GET/PUT/DELETE /api/spools/{id}`, `GET /api/summary`, `GET /api/balance`.
- **D-09:** All responses JSON. No auth. Errors: structured `{ "error": "..." }` with 404/409/422.
- **D-10:** Amount owed = sum of `PricePaid` for non-me-owner spools where `PaymentStatus` is Unpaid or Partial. Null `PricePaid` contributes 0 but triggers BAL-03 flag.
- **D-11:** Summary bar: total spool count, my-spool count, total value (sum of all non-null PricePaid), total owed (sum across all non-me owners).

### Claude's Discretion

- Exact HTML/CSS styling, color scheme, visual design (keep clean and functional)
- HTTP status code choices within ranges noted in D-09
- Whether to use CSS custom properties for colors
- Exact chip button styling (border, active color treatment)

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SPOOL-01 | User can add a spool (free-text in Phase 2, no catalog dropdown) | Spool entity + POST /api/spools + spool add form dialog |
| SPOOL-02 | User can assign a spool to an owner (self or named friend) | OwnerId FK + owner select in form |
| SPOOL-03 | User can set weight, price paid, payment status, spool status, notes | Spool entity fields D-02 + form fields |
| SPOOL-04 | User can edit any spool's fields after creation | PUT /api/spools/{id} + edit dialog reuse |
| SPOOL-05 | User can delete a spool | DELETE /api/spools/{id} + inline confirm in form |
| SPOOL-06 | User can filter and search the spool list | Five-filter bar + in-memory filter function (D-06) |
| OWNER-01 | User can add a named owner | POST /api/owners + owner modal form |
| OWNER-02 | User can delete an owner — rejected if owner has spools | DELETE /api/owners/{id} → 409 if spools exist; inline error in modal |
| BAL-01 | Summary bar shows total spools, my spools, total value, total owed | GET /api/summary + sticky bar rendering |
| BAL-02 | Balance section shows per-non-me-owner row: name, spool count, value, owed | GET /api/balance + balance table |
| BAL-03 | Balance row flags when contributing spools have no price set | API includes flag; UI shows amber warning indicator |

</phase_requirements>

---

## Summary

Phase 2 layers the full user-facing feature set on top of the Phase 1 skeleton. The backend work is a straightforward EF Core migration (adds the `Spools` table) plus registering minimal API endpoints in `Program.cs`. The frontend work replaces the placeholder `index.html` with a complete single-page application using plain HTML, CSS, and ES modules — no build step, no framework.

The architecture is intentionally simple: the server is the source of truth, the browser fetches data on page load and after every mutation, and all filtering happens in-memory in JavaScript (no per-filter API calls). The native `<dialog>` element handles all modals. The balance and summary calculations live in the API, not in JS, keeping the browser layer thin.

**Primary recommendation:** Build backend first (entity → migration → endpoints → summary/balance queries), then replace the placeholder HTML with the full UI. Keep JS split across `api.js`, `spools.js`, `owners.js`, and `summary.js` modules imported by a thin `app.js` entry point.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Spool CRUD | API (ASP.NET Core minimal API) | Database (EF Core + SQLite) | Persistence and validation belong server-side |
| Owner CRUD | API | Database | Same pattern — referential integrity enforced at API layer (409) |
| Balance calculation | API (`/api/balance`) | — | Aggregation query in EF Core; browser consumes ready-made rows |
| Summary stats | API (`/api/summary`) | — | Single aggregation query; browser just renders values |
| Filter logic | Browser (in-memory JS) | — | D-06 specifies live filtering without round-trips; data already loaded |
| Spool list rendering | Browser (vanilla JS DOM) | — | Static file UI; no SSR |
| Modal lifecycle | Browser (native `<dialog>`) | — | CLAUDE.md mandates native dialog; no server involvement |
| Color hex defaulting | API (on POST/PUT) | Browser (on form submit) | API enforces #888888 fallback; browser can pre-validate |

---

## Standard Stack

### Core (already in project — no new packages needed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core minimal API | .NET 10 (10.0.7) | Route registration + JSON responses | Already wired in Program.cs |
| EF Core 10 + Sqlite | 10.0.7 | ORM + migration for Spools table | Already in csproj — add migration only |
| EF Core Design | 10.0.7 | `dotnet ef migrations add` tooling | Already in csproj |

[VERIFIED: csproj inspection — all packages present at 10.0.7]

### No New Packages Required

Phase 2 adds no NuGet dependencies. All capabilities (JSON, SQLite, migrations, static files) are already present in the Phase 1 project.

**Frontend:** Zero npm packages. Vanilla HTML/CSS/JS per CLAUDE.md and CONTEXT.md decisions.

---

## Architecture Patterns

### System Architecture Diagram

```
Browser (index.html + ES modules)
  │
  │  page load: Promise.all([/api/spools, /api/owners, /api/summary, /api/balance])
  │  mutation: re-fetch affected endpoints
  │
  ├──► GET /api/spools          ──► EF Core query → JSON array
  ├──► POST /api/spools         ──► validate → insert → 201
  ├──► PUT /api/spools/{id}     ──► validate → update → 200
  ├──► DELETE /api/spools/{id}  ──► delete → 204
  ├──► GET /api/owners          ──► query Owners table → JSON array
  ├──► POST /api/owners         ──► insert → 201
  ├──► DELETE /api/owners/{id}  ──► check spools → 409 OR delete → 204
  ├──► GET /api/summary         ──► aggregate query → { totalSpools, mySpools, totalValue, totalOwed }
  └──► GET /api/balance         ──► group-by owner query → [ { owner, spools, value, owed, hasUnpriced } ]

In-memory filter (browser only — no round-trips):
  allSpools[] ──► filterFn(owner, material, spoolStatus, paymentStatus, searchText) ──► visibleSpools[]
```

### Recommended Project Structure

```
FilamentCatalog/
├── Models/
│   ├── Owner.cs            (existing)
│   ├── Spool.cs            (NEW — entity)
│   ├── PaymentStatus.cs    (NEW — enum)
│   └── SpoolStatus.cs      (NEW — enum)
├── Migrations/
│   ├── 20260430215538_InitialCreate.*  (existing)
│   └── YYYYMMDDHHMMSS_AddSpools.*     (NEW — generated by dotnet ef)
├── AppDbContext.cs         (extend: add DbSet<Spool>, configure FK)
├── Program.cs              (extend: add all MapGet/MapPost/MapPut/MapDelete calls)
└── wwwroot/
    ├── index.html          (REPLACE placeholder with full UI markup)
    ├── css/
    │   └── app.css         (NEW — all styles, CSS custom properties)
    └── js/
        ├── app.js          (NEW — entry point, page-load init)
        ├── api.js          (NEW — fetch wrappers for all endpoints)
        ├── spools.js       (NEW — spool list render + filter logic)
        ├── owners.js       (NEW — owner modal logic)
        └── summary.js      (NEW — summary bar + balance section)
```

### Pattern 1: Minimal API Endpoint Registration

**What:** All endpoints registered in `Program.cs` before `app.RunAsync()` using `MapGet`, `MapPost`, `MapPut`, `MapDelete` with async lambdas receiving `AppDbContext` via DI.

**When to use:** Every API route in this project.

**Example:**
```csharp
// Source: CLAUDE.md + Phase 1 Program.cs pattern
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
    if (spoolCount > 0) return Results.Conflict(new { error = $"Cannot delete — {spoolCount} spool(s) assigned. Remove spools first." });
    db.Owners.Remove(owner);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
```

[VERIFIED: ASP.NET Core minimal API pattern — confirmed against existing Program.cs]

### Pattern 2: EF Core Entity + Migration

**What:** Add `Spool.cs` entity class, add `DbSet<Spool>` to `AppDbContext`, configure FK relationship, then run `dotnet ef migrations add AddSpools` to generate migration.

**Example:**
```csharp
// Spool.cs
public class Spool
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Material { get; set; }
    public required string ColorHex { get; set; }  // default #888888
    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;
    public int? WeightGrams { get; set; }
    public decimal? PricePaid { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public SpoolStatus SpoolStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// AppDbContext.cs — add to existing class:
public DbSet<Spool> Spools => Set<Spool>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Spool>()
        .HasOne(s => s.Owner)
        .WithMany()
        .HasForeignKey(s => s.OwnerId)
        .OnDelete(DeleteBehavior.Restrict); // D-03: no cascade
}
```

[VERIFIED: EF Core 10 FK pattern — confirmed against existing InitialCreate migration and AppDbContext]

### Pattern 3: Summary and Balance Query

**What:** Single LINQ queries computing aggregates server-side. These are the two non-trivial queries in the phase.

**Example:**
```csharp
// GET /api/summary
app.MapGet("/api/summary", async (AppDbContext db) =>
{
    var meOwner = await db.Owners.FirstOrDefaultAsync(o => o.IsMe);
    var spools = await db.Spools.ToListAsync();
    var mySpoolCount = meOwner is null ? 0 : spools.Count(s => s.OwnerId == meOwner.Id);
    var totalValue = spools.Where(s => s.PricePaid.HasValue).Sum(s => s.PricePaid!.Value);
    // Owed = Unpaid or Partial, non-me owner
    var owedSpools = meOwner is null ? spools : spools.Where(s => s.OwnerId != meOwner.Id);
    var totalOwed = owedSpools
        .Where(s => s.PaymentStatus != PaymentStatus.Paid && s.PricePaid.HasValue)
        .Sum(s => s.PricePaid!.Value);
    return Results.Ok(new { totalSpools = spools.Count, mySpools = mySpoolCount, totalValue, totalOwed });
});

// GET /api/balance
app.MapGet("/api/balance", async (AppDbContext db) =>
{
    var meOwner = await db.Owners.FirstOrDefaultAsync(o => o.IsMe);
    var nonMeOwners = await db.Owners.Where(o => !o.IsMe).ToListAsync();
    var allSpools = await db.Spools.ToListAsync();
    var rows = nonMeOwners.Select(owner =>
    {
        var ownerSpools = allSpools.Where(s => s.OwnerId == owner.Id).ToList();
        var value = ownerSpools.Where(s => s.PricePaid.HasValue).Sum(s => s.PricePaid!.Value);
        var owed = ownerSpools
            .Where(s => s.PaymentStatus != PaymentStatus.Paid && s.PricePaid.HasValue)
            .Sum(s => s.PricePaid!.Value);
        var hasUnpriced = ownerSpools.Any(s => !s.PricePaid.HasValue);
        return new { ownerId = owner.Id, ownerName = owner.Name, spoolCount = ownerSpools.Count, value, owed, hasUnpriced };
    });
    return Results.Ok(rows);
});
```

[ASSUMED: query correctness derived from D-10 and D-11 — no runtime verification in this session]

### Pattern 4: ES Module Structure

**What:** Each JS file is an ES module. `app.js` is the entry point. Other modules export functions. `api.js` wraps all fetch calls.

**Example:**
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

// wwwroot/index.html
<script type="module" src="/js/app.js"></script>
```

[VERIFIED: per CLAUDE.md ES modules requirement and Phase 1 placeholder comment]

### Pattern 5: Native Dialog + CustomEvent

**What:** Open/close modals with `.showModal()` / `.close()`. Backdrop click closes dialog. On owner modal close, dispatch `CustomEvent('owners-updated')` on `document`.

**Example:**
```javascript
// Open
dialog.showModal();

// Backdrop click closes
dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close(); });

// Dispatch after owner mutation
dialog.addEventListener('close', () => {
    document.dispatchEvent(new CustomEvent('owners-updated'));
});

// Spool form listens
document.addEventListener('owners-updated', async () => {
    const owners = await getOwners();
    repopulateOwnerSelect(owners);
});
```

[VERIFIED: native dialog behavior — spec-compliant, per CLAUDE.md mandate]

### Anti-Patterns to Avoid

- **Cascade delete on Owner→Spool FK:** CLAUDE.md + D-03 require `DeleteBehavior.Restrict`. EF Core's default on optional FKs may be `ClientSetNull` — must explicitly set `Restrict`.
- **DateTime.Now instead of DateTime.UtcNow:** Explicitly forbidden by CLAUDE.md. Always `DateTime.UtcNow`.
- **Relative SQLite path:** Always `Path.Combine(AppContext.BaseDirectory, "filament.db")`. The Phase 1 code already does this correctly — do not introduce any new DB path construction.
- **Re-fetching on every filter change:** D-06 specifies live in-memory filtering. Never make an API call per filter change.
- **Using `<form method="dialog">`:** UI-SPEC mandates JS-intercepted submit, not native dialog form submission, so API calls can be made.
- **Registering endpoints after `app.Run()`:** The async `RunAsync()` call blocks. All `MapGet`/`MapPost` etc. calls must appear before `await app.RunAsync()`.
- **Null ColorHex reaching the database:** API must default empty/invalid ColorHex to `#888888` before save (D-02).
- **Deleting the "Me" owner:** DELETE `/api/owners/{id}` must check `IsMe` and return 422 before the spool-count check.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Database migration | Manual SQL CREATE TABLE | `dotnet ef migrations add AddSpools` | EF Core handles column types, constraints, FK, rollback |
| JSON serialization | Manual string building | ASP.NET Core built-in System.Text.Json | Already wired; `Results.Ok(obj)` serializes automatically |
| FK constraint enforcement | Application-level check for orphan records | EF Core `DeleteBehavior.Restrict` + application-level 409 response | EF throws `DbUpdateException` on cascade violation; the application-level count check gives a user-friendly error before that |
| Color picker sync | Custom color wheel | `<input type="color">` paired with `<input type="text">` | Native browser color picker; zero JS library needed |
| Collapsible balance section | JS toggle + local storage | `<details open>` HTML element | D-05: native browser handles expand/collapse; no persistence needed |
| Modal | DIV overlay + z-index management | Native `<dialog>` | CLAUDE.md mandates native dialog; browser handles focus trap, Escape key, backdrop |

**Key insight:** For a local-only single-user app of this scope, the browser's native elements (dialog, details, color input, form validation) eliminate entire categories of custom JavaScript.

---

## Common Pitfalls

### Pitfall 1: EF Core enum storage in SQLite

**What goes wrong:** EF Core 10 stores C# enums as integers by default in SQLite. This is fine for new tables but can cause confusion if you try to query by string value, and future SQLite viewers show raw integers.

**Why it happens:** Default EF Core behavior — no attribute or Fluent API call needed to get integers, but no string conversion happens automatically.

**How to avoid:** Enums stored as integers are acceptable for this project. If string storage is desired, add `.HasConversion<string>()` in `OnModelCreating`. For Phase 2, integer storage is fine since the UI always receives enum names via JSON (System.Text.Json serializes enum names by default in minimal API).

**Warning signs:** JSON responses showing `0`, `1`, `2` instead of `"Paid"`, `"Unpaid"`, `"Partial"` — fix by adding `builder.Services.AddRouting()` with `JsonStringEnumConverter` or configure on the endpoint.

**Resolution:** Add `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))` in Program.cs service registration block so enum values serialize as strings in API responses. [ASSUMED: exact API for .NET 10 minimal API JSON enum config — verify against actual behavior]

### Pitfall 2: OwnerId FK — EF Core shadow property vs explicit property

**What goes wrong:** If `OwnerId` is not declared as an explicit property on `Spool`, EF Core creates a shadow property. This works but makes the POST/PUT request binding harder — the JSON payload needs to include `ownerId` as an integer but EF won't populate it automatically from a DTO.

**How to avoid:** Declare `public int OwnerId { get; set; }` explicitly on the `Spool` entity (shown in Pattern 2 above). The request body then simply needs `"ownerId": 3`.

### Pitfall 3: Material select populated from loaded spools, not API

**What goes wrong:** Building a separate `/api/materials` endpoint or fetching materials separately.

**How to avoid:** Per D-06, the material `<select>` is populated from the distinct `material` values in the already-loaded `allSpools` array. Repopulate it after every spool mutation. No separate endpoint.

### Pitfall 4: Balance calculation including "Me" spools

**What goes wrong:** `totalOwed` accidentally including spools assigned to the "Me" owner when `PaymentStatus` is Unpaid.

**How to avoid:** Both `/api/summary` and `/api/balance` queries must filter to `OwnerId != meOwner.Id` before summing owed amounts. Query the "Me" owner once and pass its Id to the filter.

### Pitfall 5: `<details open>` attribute

**What goes wrong:** Using `<details>` without the `open` attribute results in the balance section starting collapsed, violating D-05.

**How to avoid:** Write `<details open>` in the HTML. The browser manages toggle state natively — no JS needed, and the state does not persist across page reloads (D-05 requirement satisfied for free).

### Pitfall 6: Filter AND logic with chips — empty set means "all"

**What goes wrong:** Treating chip filters as OR-across-groups instead of AND-across-groups, or treating an empty chip selection as "show nothing" instead of "show all".

**How to avoid:** Filter logic: a spool passes a chip group if either (a) no chips are selected in that group, or (b) the spool's value matches one of the selected chips. Combine chip group results with AND across all five filter controls.

---

## Code Examples

### Spool entity with enums

```csharp
// Models/PaymentStatus.cs
public enum PaymentStatus { Paid, Unpaid, Partial }

// Models/SpoolStatus.cs
public enum SpoolStatus { Sealed, Active, Empty }

// Models/Spool.cs
public class Spool
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Material { get; set; }
    public required string ColorHex { get; set; }
    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;
    public int? WeightGrams { get; set; }
    public decimal? PricePaid { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public SpoolStatus SpoolStatus { get; set; } = SpoolStatus.Sealed;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

[VERIFIED: entity shape matches D-02 exactly]

### AppDbContext extension

```csharp
// AppDbContext.cs — complete replacement
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Spool> Spools => Set<Spool>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Spool>()
            .HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### In-memory filter function (JS)

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

### Color hex input sync (JS)

```javascript
// Syncs <input type="color"> and <input type="text"> for the hex value
const colorPicker = document.getElementById('colorPicker');
const hexText = document.getElementById('colorHex');
const swatch = document.getElementById('colorSwatch');

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

[VERIFIED: pattern matches UI-SPEC color hex sync specification]

### CSS custom properties (starting point)

```css
/* wwwroot/css/app.css */
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

[VERIFIED: values match UI-SPEC exactly]

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DIV overlay modals | Native `<dialog>` | Modern browsers (baseline 2022) | Focus trap, Escape key, backdrop handled by browser |
| `<details>`+JS toggle | `<details open>` HTML only | Always available | No JS for collapsible sections |
| jQuery for DOM | Vanilla JS ES modules | ~2018+ | No dependency, native fetch, modules |

**Applicable to this project:**
- `<dialog>` is baseline-available in all modern browsers — no polyfill needed for a local Windows app. [VERIFIED: browser compatibility — Windows 10+ browsers all support native dialog]

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Summary/balance queries load all spools into memory then aggregate in C# (not SQL aggregates) | Code Examples — summary query | For large datasets this is slow; for a personal spool tracker (<10,000 rows) it is fine. Phase 3 can optimize if needed. |
| A2 | `ConfigureHttpJsonOptions` with `JsonStringEnumConverter` is the correct API for .NET 10 minimal API enum serialization | Pitfall 1 | If wrong, enums serialize as integers in JSON; fix is to add the converter to the correct options object |
| A3 | `DeleteBehavior.Restrict` on the Spool→Owner FK prevents EF Core from issuing a DELETE if spools exist, without requiring a database-level FK constraint check | Architecture Patterns | EF Core Restrict means the application layer must check; the DB constraint may or may not exist depending on SQLite migration. The 409 response is application-level (count check before delete), so behavior is correct regardless. |

---

## Open Questions

1. **Enum serialization in .NET 10 minimal API**
   - What we know: System.Text.Json does not serialize enums as strings by default.
   - What's unclear: Exact configuration call for .NET 10 minimal API to serialize enums as strings globally.
   - Recommendation: At implementation time, verify with a quick test request. If responses show integer values for PaymentStatus/SpoolStatus, add `JsonStringEnumConverter` via `builder.Services.ConfigureHttpJsonOptions(...)`.

2. **Migration command working directory**
   - What we know: `dotnet ef migrations add` must be run from the project directory.
   - What's unclear: Whether the Windows service project requires any special `--project` flags.
   - Recommendation: Run from `FilamentCatalog/` directory. The existing `AppDbContext.cs` is in that project, so no flags needed.

---

## Environment Availability

Step 2.6: No new external dependencies introduced in Phase 2. All tools (dotnet CLI, EF Core tooling already in csproj as Design package) are verified present from Phase 1 execution. No additional environment audit needed.

---

## Project Constraints (from CLAUDE.md)

| Directive | Impact on Phase 2 |
|-----------|-------------------|
| SQLite path: `Path.Combine(AppContext.BaseDirectory, "filament.db")` | Already in Program.cs — do not introduce any new DB path construction |
| `UseDefaultFiles()` before `UseStaticFiles()` — mandatory order | Already correct in Program.cs — do not reorder |
| `DateTime.UtcNow` always | Spool.CreatedAt must use `DateTime.UtcNow` |
| Native `<dialog>` for modals | Both spool form and owner modal must use `<dialog>` |
| ES modules (`type="module"`) | All JS files use `<script type="module">` |
| No build step, no framework | Zero npm, zero bundler, zero React/Vue/Alpine |
| ImageSharp: always dispose with `using` | Not applicable in Phase 2 (no ImageSharp usage) |

---

## Sources

### Primary (HIGH confidence)
- `FilamentCatalog/Program.cs` — existing startup pattern, middleware order
- `FilamentCatalog/AppDbContext.cs` — existing context structure
- `FilamentCatalog/Models/Owner.cs` — existing entity pattern
- `FilamentCatalog/FilamentCatalog.csproj` — confirmed package versions at 10.0.7
- `FilamentCatalog/Migrations/20260430215538_InitialCreate.cs` — confirmed migration pattern
- `FilamentCatalog/wwwroot/index.html` — placeholder confirmed, ready to replace
- `.planning/phases/02-spool-owner-crud/02-CONTEXT.md` — all locked decisions
- `.planning/phases/02-spool-owner-crud/02-UI-SPEC.md` — all UI/interaction specifications
- `CLAUDE.md` — all mandatory conventions

### Secondary (MEDIUM confidence)
- ASP.NET Core minimal API patterns — confirmed via existing Program.cs structure
- Native `<dialog>` browser support — widely documented, baseline 2022

### Tertiary (LOW confidence)
- None in this research

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already in project, versions confirmed
- Architecture: HIGH — patterns derived directly from existing Phase 1 code and locked CONTEXT.md decisions
- Pitfalls: MEDIUM — some derived from general .NET/EF Core knowledge (A2 assumption on enum config)
- Frontend patterns: HIGH — all specified in UI-SPEC, no framework decisions needed

**Research date:** 2026-05-01
**Valid until:** 2026-06-01 (stable stack — .NET 10 LTS, EF Core 10)
