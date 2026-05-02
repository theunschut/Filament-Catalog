# Phase 3: Bambu Catalog Sync - Pattern Map

**Mapped:** 2026-05-02
**Files analyzed:** 11 new/modified files
**Analogs found:** 10 / 11

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `FilamentCatalog.EntityFramework/Models/BambuProduct.cs` | model | CRUD | `Spool.cs` | exact |
| `FilamentCatalog.EntityFramework/AppDbContext.cs` | config | CRUD | Existing (read) | role-match |
| `FilamentCatalog.Service/Services/ISyncService.cs` | service interface | request-response | `IOwnerService.cs` | role-match |
| `FilamentCatalog.Service/Services/SyncService.cs` | service | request-response | `OwnerService.cs` | role-match |
| `FilamentCatalog.Service/Services/SyncStateService.cs` | service (singleton) | request-response | None | no-analog |
| `FilamentCatalog.Service/Services/SyncBackgroundService.cs` | background service | request-response | None | no-analog |
| `FilamentCatalog.Service/Controllers/SyncController.cs` | controller | request-response | `OwnersController.cs` | exact |
| `FilamentCatalog.Service/wwwroot/js/catalog.js` | ES module | request-response | `owners.js` | role-match |
| `FilamentCatalog.Service/wwwroot/js/spools.js` | ES module (extend) | request-response | Existing (read) | role-match |
| `FilamentCatalog.Service/wwwroot/js/api.js` | ES module (extend) | request-response | Existing (read) | role-match |
| `FilamentCatalog.Service/wwwroot/index.html` | HTML (extend) | request-response | Existing (read) | role-match |
| `FilamentCatalog.Service/wwwroot/app.css` | CSS (extend) | request-response | Existing (read) | role-match |

---

## Pattern Assignments

### `FilamentCatalog.EntityFramework/Models/BambuProduct.cs` (model, CRUD)

**Analog:** `FilamentCatalog.EntityFramework/Models/Spool.cs`

**Pattern: Entity class structure** (lines 1-15):
```csharp
// Source: Spool.cs — simple entity with properties and FK references
public class BambuProduct
{
    public int Id { get; set; }
    public required string Name { get; set; }          // Product title from Shopify
    public required string Material { get; set; }      // Variant option value
    public required string ColorName { get; set; }     // Color variant title
    public required string ColorHex { get; set; }      // Extracted dominant color
    public string? ColorSwatchUrl { get; set; }        // Shopify image URL (optional)
    public DateTime LastSyncedAt { get; set; }         // UTC timestamp (use DateTime.UtcNow per CLAUDE.md)
}
```

**Key convention:** All `DateTime` fields use `DateTime.UtcNow` — see `Spool.cs` line 14 (`CreatedAt = DateTime.UtcNow`). Entity has no navigation properties or FK (BambuProduct is the source of truth, not referenced by Spool).

---

### `FilamentCatalog.EntityFramework/AppDbContext.cs` (config, CRUD)

**Analog:** Existing `AppDbContext.cs` (lines 1-18)

**Pattern: DbContext with DbSet addition** (lines 7-8):
```csharp
public DbSet<Owner> Owners => Set<Owner>();
public DbSet<Spool> Spools => Set<Spool>();
// ADD: public DbSet<BambuProduct> BambuProducts => Set<BambuProduct>();
```

**Migration pattern:** Follow the migration timestamp structure seen in existing migrations (e.g., `20260430215538_InitialCreate.cs`). Add BambuProduct migration with:
- Composite unique key on (Name, Material) for upsert matching (per CONTEXT.md D-SYNC-04)
- Indexes on Material (for catalog picker filtering) and LastSyncedAt (for query optimization)

---

### `FilamentCatalog.Service/Services/ISyncService.cs` (service interface, request-response)

**Analog:** `FilamentCatalog.Service/Services/IOwnerService.cs`

**Pattern: Service interface** (lines 1-6):
```csharp
// Source: IOwnerService.cs — simple async task methods
public interface IOwnerService
{
    Task<List<Owner>> GetAllAsync();
    Task<Owner> CreateAsync(string name);
    Task DeleteAsync(int id);
}

// Apply same pattern for ISyncService:
public interface ISyncService
{
    Task SyncCatalogAsync(CancellationToken cancellationToken);
}
```

**Key convention:** 
- Single responsibility: one core method `SyncCatalogAsync()` 
- Takes `CancellationToken` for graceful shutdown (used by BackgroundService)
- Throws custom exceptions (DomainValidationException, etc.) caught by controller

---

### `FilamentCatalog.Service/Services/SyncService.cs` (service, request-response)

**Analog:** `FilamentCatalog.Service/Services/OwnerService.cs`

**Pattern: Constructor injection + async methods** (lines 1-30):
```csharp
// Source: OwnerService.cs lines 1-16 — constructor injection pattern
using Microsoft.EntityFrameworkCore;

public class OwnerService(AppDbContext db) : IOwnerService
{
    public Task<List<Owner>> GetAllAsync() =>
        db.Owners.OrderBy(o => o.CreatedAt).ToListAsync();

    public async Task<Owner> CreateAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Name is required.");
        var owner = new Owner { Name = name.Trim(), IsMe = false, CreatedAt = DateTime.UtcNow };
        db.Owners.Add(owner);
        await db.SaveChangesAsync();
        return owner;
    }
}

// Apply same pattern for SyncService:
// - Inject AppDbContext, ILogger<SyncService>, HttpClient, SyncStateService
// - Implement SyncCatalogAsync() with try/catch blocks calling SyncStateService.Start(), IncrementProgress(), Complete()
// - Fetch Shopify /products.json with cursor pagination (per RESEARCH.md Pattern 1)
// - For each variant: download swatch image, extract color (per RESEARCH.md Pattern 2), upsert to BambuProduct table
```

**Error handling pattern** (lines 19-21):
```csharp
catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
```

**Database operations pattern** (lines 18-32):
```csharp
var spool = new Spool { ... CreatedAt = DateTime.UtcNow };
db.Spools.Add(spool);
await db.SaveChangesAsync();
```

**Key conventions:**
- Inject `AppDbContext db` as constructor parameter
- Use `DateTime.UtcNow` for all timestamps (CLAUDE.md line 22)
- Upsert via `DbContext.Update()` — match on (Name, Material) composite key
- Call `SyncStateService` methods to track progress (Start, IncrementProgress, Complete, Error)
- Log all major steps using injected `ILogger<SyncService>`

---

### `FilamentCatalog.Service/Services/SyncStateService.cs` (service, request-response, singleton)

**Analog:** None (new pattern per RESEARCH.md Pattern 4)

**Pattern: Singleton state manager** (from RESEARCH.md lines 420-460):
```csharp
public class SyncStateService
{
    private string _status = "idle";
    private int _processedCount = 0;
    private int _totalEstimate = 0;
    private string? _errorMessage = null;
    private DateTime? _lastSyncedAt = null;

    public SyncStatusDto GetStatus() =>
        new SyncStatusDto
        {
            Status = _status,
            ProcessedCount = _processedCount,
            TotalEstimate = _totalEstimate,
            ErrorMessage = _errorMessage,
            LastSyncedAt = _lastSyncedAt
        };

    public void Start(int? totalEstimate = null)
    {
        _status = "running";
        _processedCount = 0;
        _totalEstimate = totalEstimate ?? 0;
        _errorMessage = null;
    }

    public void IncrementProgress() => _processedCount++;

    public void Complete(DateTime syncTime)
    {
        _status = "completed";
        _lastSyncedAt = syncTime;
    }

    public void Error(string message)
    {
        _status = "error";
        _errorMessage = message;
    }
}
```

**SyncStatusDto pattern** (RESEARCH.md lines 407-417):
```csharp
public class SyncStatusDto
{
    public string Status { get; set; } // "idle", "running", "completed", "error"
    public int ProcessedCount { get; set; }
    public int? TotalEstimate { get; set; }
    public int? PercentComplete => TotalEstimate.HasValue && TotalEstimate > 0
        ? (int)((ProcessedCount * 100) / TotalEstimate)
        : null;
    public string? ErrorMessage { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
```

**Key conventions:**
- Register in `Program.cs` as singleton: `builder.Services.AddSingleton<SyncStateService>();`
- Thread-safe (use `lock()` if concurrent access expected, but current Channel model has single consumer)
- Always reset state in `Start()` to clear stale values between sync runs (per RESEARCH.md Pitfall 2)

---

### `FilamentCatalog.Service/Services/SyncBackgroundService.cs` (background service, request-response)

**Analog:** None (new pattern per RESEARCH.md Pattern 3)

**Pattern: BackgroundService + Channel consumer** (from RESEARCH.md lines 339-371):
```csharp
public record SyncJob(int Id = 0);

public class SyncBackgroundService : BackgroundService
{
    private readonly Channel<SyncJob> _channel;
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(
        Channel<SyncJob> channel,
        ISyncService syncService,
        ILogger<SyncBackgroundService> logger)
    {
        _channel = channel;
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consume jobs from channel until cancelled
        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Starting sync job {JobId}", job.Id);
                await _syncService.SyncCatalogAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync job {JobId} failed", job.Id);
            }
        }
    }
}
```

**Program.cs registration** (from RESEARCH.md lines 328-336):
```csharp
var channel = Channel.CreateBounded<SyncJob>(
    new BoundedChannelOptions(capacity: 1)
    {
        FullMode = BoundedChannelFullMode.DropNewest
    });

builder.Services.AddSingleton(channel);
builder.Services.AddHostedService<SyncBackgroundService>();
builder.Services.AddSingleton<SyncStateService>();
```

**Key conventions:**
- Use `System.Threading.Channels` (built-in, no NuGet) with capacity 1 and DropNewest mode (per CLAUDE.md)
- BackgroundService runs continuously until app shuts down
- Channel is empty after job processed; next POST /api/sync/start enqueues another job
- Catch all exceptions to prevent service crash; log errors for diagnostics

---

### `FilamentCatalog.Service/Controllers/SyncController.cs` (controller, request-response)

**Analog:** `FilamentCatalog.Service/Controllers/OwnersController.cs`

**Pattern: ApiController with try/catch** (lines 1-34):
```csharp
// Source: OwnersController.cs
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OwnersController(IOwnerService ownerService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await ownerService.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create(OwnerCreateRequest req)
    {
        try
        {
            var owner = await ownerService.CreateAsync(req.Name);
            return Created($"/api/owners/{owner.Id}", owner);
        }
        catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
    }
}

// Apply pattern to SyncController:
[ApiController]
[Route("api/[controller]")]
public class SyncController(Channel<SyncJob> channel, SyncStateService stateService) : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> StartSync()
    {
        var job = new SyncJob(Id: Environment.TickCount);
        await _channel.Writer.WriteAsync(job);
        return Accepted();  // 202 Accepted per RESEARCH.md Pattern 4
    }

    [HttpGet("status")]
    public IActionResult GetStatus() =>
        Ok(_stateService.GetStatus());  // Returns SyncStatusDto
}
```

**Key conventions:**
- Inject via constructor: `Channel<SyncJob>`, `SyncStateService`
- POST /api/sync/start returns 202 Accepted immediately (no wait for background job)
- GET /api/sync/status returns current SyncStatusDto (client polls every 500ms per RESEARCH.md)
- No explicit error handling needed (background job errors are logged, not exposed to API)

---

### `FilamentCatalog.Service/wwwroot/js/catalog.js` (ES module, request-response)

**Analog:** `FilamentCatalog.Service/wwwroot/js/owners.js`

**Pattern: ES module with event handlers and API calls** (lines 1-100):
```javascript
// Source: owners.js lines 1-100 — modal structure and error handling

import { getOwners, createOwner, deleteOwner } from './api.js';

const dialog    = document.getElementById('owner-dialog');
const listEl    = document.getElementById('owner-list');
const errorEl   = document.getElementById('owner-error');
const nameInput = document.getElementById('owner-name-input');
const addBtn    = document.getElementById('owner-add-btn');

function showError(message) {
    errorEl.textContent = message;
    errorEl.style.display = 'block';
}

function clearError() {
    errorEl.textContent = '';
    errorEl.style.display = 'none';
}

// Export initialization function
export function initOwnerModal() {
    gearBtn.addEventListener('click', openModal);
}

// Apply same structure for catalog.js two-step picker:
// - Import getCatalogMaterials, getCatalogColors from api.js
// - DOM refs: materialSelect, colorSelect, nameInput, colorHexInput
// - Event handlers: materialSelect.addEventListener('change', async () => { populate colors })
// - Event handlers: colorSelect.addEventListener('change', () => { auto-fill name and hex })
// - Export initializeCatalogSelects() and restoreCatalogSelectsFromSpool() for spools.js to call
```

**Material-to-color population pattern** (from RESEARCH.md lines 559-598):
```javascript
materialSelect.addEventListener('change', async () => {
    colorSelect.innerHTML = '<option value="">— Select color —</option>';
    if (!materialSelect.value) return;

    try {
        const colors = await getCatalogColors(materialSelect.value);
        colors.forEach(color => {
            const opt = document.createElement('option');
            opt.value = color.id;
            opt.dataset.colorName = color.colorName;
            opt.dataset.colorHex = color.colorHex;
            opt.dataset.productTitle = color.productTitle;
            opt.textContent = color.colorName;
            colorSelect.appendChild(opt);
        });
    } catch (err) {
        console.error('Failed to load colors', err);
    }
});

colorSelect.addEventListener('change', () => {
    if (!colorSelect.value) {
        nameInput.value = '';
        colorHexInput.value = '#888888';
        return;
    }

    const opt = colorSelect.selectedOptions[0];
    const productTitle = opt.dataset.productTitle;
    const colorName = opt.dataset.colorName;
    const colorHex = opt.dataset.colorHex;

    // Format: "Product Title — Color Name" (per CONTEXT.md D-02)
    nameInput.value = `${productTitle} — ${colorName}`;
    colorHexInput.value = colorHex;
});
```

**Key conventions:**
- All DOM queries use `getElementById()` or `querySelector()` (safe, no user input in selectors)
- All user data rendered via `textContent` (not `innerHTML`) to prevent XSS (owners.js line 30)
- Error messages shown via `.style.display` toggle (owners.js lines 12-20)
- Export `initXxx()` functions for app.js to call during page load

---

### `FilamentCatalog.Service/wwwroot/js/spools.js` (ES module, extend, request-response)

**Analog:** Existing `FilamentCatalog.Service/wwwroot/js/spools.js` (modify in-place)

**Changes to make:**

1. **Replace material input (lines 26, 90, 282, 316) with two-step selects:**
   - Change from: `<input type="text" id="spool-material" />`
   - Change to: Two `<select>` elements in index.html (spool-catalog-material, spool-catalog-color)
   - Update DOM refs in spools.js to point to new selects

2. **Add catalog initialization in openAddDialog() (line 298):**
   ```javascript
   function openAddDialog() {
       resetFormForAdd();
       repopulateOwnerSelect(allOwners);
       // NEW: Initialize catalog selects
       initializeCatalogSelects();
       dialog.showModal();
   }
   ```

3. **Update openEditDialog() to restore two-step selects (line 304):**
   ```javascript
   function openEditDialog(spool) {
       resetFormForAdd();
       repopulateOwnerSelect(allOwners);
       populateFormForEdit(spool);
       // NEW: Restore catalog selects from saved spool data
       restoreCatalogSelectsFromSpool(spool);
       dialog.showModal();
   }
   ```

4. **Update buildSpoolPayload() (line 333) to use materialSelect instead of matInput:**
   ```javascript
   function buildSpoolPayload() {
       return {
           // ... existing fields ...
           material: materialSelect.value.trim(),  // Changed from matInput
           // ... rest unchanged ...
       };
   }
   ```

5. **Import from catalog.js at top (line 3):**
   ```javascript
   import { initializeCatalogSelects, restoreCatalogSelectsFromSpool } from './catalog.js';
   ```

6. **Disable Add Spool button when catalog is empty (new check in app.js):**
   - Check on page load: `GET /api/catalog/count`
   - If count === 0, set `addBtn.disabled = true` and show banner
   - Listen for 'sync-complete' event to re-enable and hide banner

**Key convention:** Import functions from catalog.js; spools.js orchestrates the two-step picker integration.

---

### `FilamentCatalog.Service/wwwroot/js/api.js` (ES module, extend, request-response)

**Analog:** Existing `FilamentCatalog.Service/wwwroot/js/api.js` (modify in-place)

**Changes to make — Add sync and catalog API wrappers at end of file (after line 91):**

```javascript
// ---- Sync ----

export async function startSync() {
    const res = await fetch('/api/sync/start', { method: 'POST' });
    if (!res.ok) throw new Error(`POST /api/sync/start failed: ${res.status}`);
    // 202 Accepted — no body to parse
}

export async function getSyncStatus() {
    const res = await fetch('/api/sync/status');
    if (!res.ok) throw new Error(`GET /api/sync/status failed: ${res.status}`);
    return res.json();  // Returns SyncStatusDto
}

// ---- Catalog ----

export async function getCatalogCount() {
    const res = await fetch('/api/catalog/count');
    if (!res.ok) throw new Error(`GET /api/catalog/count failed: ${res.status}`);
    return res.json();  // Returns { count: number }
}

export async function getCatalogMaterials() {
    const res = await fetch('/api/catalog/materials');
    if (!res.ok) throw new Error(`GET /api/catalog/materials failed: ${res.status}`);
    return res.json();  // Returns array of material strings
}

export async function getCatalogColors(material) {
    const res = await fetch(`/api/catalog/colors?material=${encodeURIComponent(material)}`);
    if (!res.ok) throw new Error(`GET /api/catalog/colors failed: ${res.status}`);
    return res.json();  // Returns array of { id, colorName, colorHex, productTitle }
}
```

**Pattern followed** (lines 1-92):
- Async function, throw Error on !res.ok
- Try-catch error handling in caller, not wrapper (api.js is stateless)
- Use `encodeURIComponent()` for query params
- Return parsed JSON or undefined for 204 responses

**Key convention:** All wrappers follow existing pattern (lines 6-23, 36-75).

---

### `FilamentCatalog.Service/wwwroot/index.html` (HTML, extend, request-response)

**Analog:** Existing `FilamentCatalog.Service/wwwroot/index.html` (modify in-place)

**Changes to make:**

1. **Add sync button + last synced timestamp to sticky header (after line 31, before closing `</header>`):**
   ```html
   <div class="stat-cell">
     <span class="stat-label">Last Synced</span>
     <span class="stat-value" id="stat-last-synced">—</span>
   </div>
   <button id="sync-catalog-btn" type="button">Sync Catalog</button>
   ```

2. **Add catalog-empty notice banner below filter bar (after line 55, before `<main id="spool-list">`):**
   ```html
   <div id="catalog-empty-notice" style="display:none" class="info-banner">
     <p>Bambu catalog is empty. Click the Sync Catalog button to get started.</p>
   </div>
   ```

3. **Add two-step selects to spool-dialog (replace lines 88-91 in form):**
   ```html
   <!-- Remove old single material input -->
   <!-- <div class="form-group">
     <label for="spool-material">Material <span aria-hidden="true">*</span></label>
     <input type="text" id="spool-material" required placeholder="e.g. PLA" />
   </div> -->

   <!-- Add two-step selects -->
   <div class="form-group">
     <label for="spool-catalog-material">Material <span aria-hidden="true">*</span></label>
     <select id="spool-catalog-material" required>
       <option value="">— Select material —</option>
     </select>
   </div>

   <div class="form-group">
     <label for="spool-catalog-color">Color <span aria-hidden="true">*</span></label>
     <select id="spool-catalog-color" required>
       <option value="">— Select color —</option>
     </select>
   </div>
   ```

**Key convention:** Sticky header uses flexbox (line 30); new sync button and stat-cell fit naturally into existing layout.

---

### `FilamentCatalog.Service/wwwroot/app.css` (CSS, extend, request-response)

**Analog:** Existing `FilamentCatalog.Service/wwwroot/css/app.css` (modify in-place)

**Changes to make — Add styles at end of file (after line 162):**

```css
/* ---- Sync button (header) ---- */
#sync-catalog-btn {
    height: 36px; padding: 0 var(--space-md);
    background: var(--color-accent); color: #fff; border: none; border-radius: 6px;
    font-size: 14px; font-weight: 600;
}
#sync-catalog-btn:hover { background: #1d4ed8; }
#sync-catalog-btn:disabled { background: var(--color-muted); cursor: not-allowed; }

/* ---- Info banner (catalog empty notice) ---- */
.info-banner {
    background: #dbeafe; border: 1px solid #93c5fd; color: #1e40af;
    padding: var(--space-sm) var(--space-md); border-radius: 6px; font-size: 14px; margin: var(--space-sm) var(--space-md);
}
.info-banner p { margin: 0; }

/* ---- Two-step selects styling (same as other selects in form-group) ---- */
#spool-catalog-material, #spool-catalog-color {
    height: 36px; border: 1px solid var(--color-border); border-radius: 6px;
    padding: 0 var(--space-sm); background: var(--color-surface);
}
```

**Pattern followed** (lines 1-162):
- CSS variables for colors and spacing (`:root` tokens, lines 2-16)
- Design tokens applied consistently (--color-accent, --color-muted, --space-sm, etc.)
- Hover states for interactive elements
- Uses existing component styles (.info-banner mirrors .error-banner, line 150)

**Key convention:** No new tokens introduced; reuse existing design system.

---

## Shared Patterns

### Exception Handling (applies to all services and controllers)

**Source:** `FilamentCatalog.Service/Services/OwnerService.cs` + `FilamentCatalog.Service/Controllers/OwnersController.cs`

**Pattern:**
```csharp
// Service layer: throw custom exceptions
public async Task<Owner> CreateAsync(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new DomainValidationException("Name is required.");
    // ... create and save ...
}

// Controller layer: catch and return appropriate HTTP status
[HttpPost]
public async Task<IActionResult> Create(OwnerCreateRequest req)
{
    try
    {
        var owner = await ownerService.CreateAsync(req.Name);
        return Created($"/api/owners/{owner.Id}", owner);
    }
    catch (DomainValidationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
    catch (NotFoundException ex) { return NotFound(new { error = ex.Message }); }
    catch (ConflictException ex) { return Conflict(new { error = ex.Message }); }
}
```

**Apply to:**
- `SyncController` — no try/catch needed (background job handles errors)
- `CatalogController` — catch any DB/service exceptions, return 500

---

### DateTime Convention (applies to all entity models and services)

**Source:** `CLAUDE.md` line 22 and `FilamentCatalog.EntityFramework/Models/Spool.cs` line 14

**Pattern:**
```csharp
// ✓ Correct
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // Property initializer
spool.CreatedAt = DateTime.UtcNow;  // In service

// ✗ Wrong
public DateTime CreatedAt { get; set; } = DateTime.Now;  // Local time — breaks EF Core 10 SQLite
```

**Apply to:**
- `BambuProduct.LastSyncedAt` — always set to `DateTime.UtcNow` when updating
- `SyncStateService.Complete()` — pass `DateTime.UtcNow` to record sync completion time

---

### ES Module Import Pattern (applies to all JavaScript files)

**Source:** `FilamentCatalog.Service/wwwroot/js/spools.js` line 3

**Pattern:**
```javascript
import { getSpools, createSpool, updateSpool, deleteSpool } from './api.js';

// ... module code ...

export function renderSpools(spools, owners) { ... }
export function initSpoolDialog() { ... }
```

**Apply to:**
- `catalog.js` — import getCatalogMaterials, getCatalogColors from api.js; export initializeCatalogSelects(), restoreCatalogSelectsFromSpool()
- `api.js` — export new functions: startSync(), getSyncStatus(), getCatalogCount(), getCatalogMaterials(), getCatalogColors()

---

### DOM Rendering Safety (applies to all JavaScript rendering)

**Source:** `FilamentCatalog.Service/wwwroot/js/owners.js` lines 30, 198; `spools.js` lines 168-171

**Pattern — Use textContent for user data:**
```javascript
// ✓ Correct — safe from XSS
nameEl.textContent = owner.name;

// ✗ Wrong — vulnerable to injection
nameEl.innerHTML = `<span>${owner.name}</span>`;
```

**Apply to:**
- `catalog.js` color option rendering — use `opt.textContent = color.colorName;` not `innerHTML`
- All new DOM manipulation in catalog.js and modified spools.js

---

### Error Display Pattern (applies to all dialogs)

**Source:** `FilamentCatalog.Service/wwwroot/js/spools.js` lines 252-260; `owners.js` lines 12-20

**Pattern:**
```javascript
function showDialogError(message) {
    errorEl.textContent = message;
    errorEl.style.display = 'block';
}

function clearDialogError() {
    errorEl.textContent = '';
    errorEl.style.display = 'none';
}
```

**Apply to:**
- `catalog.js` — if adding error display for failed material/color loads, follow this pattern
- `spools.js` — already in place, reuse for validation errors

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `FilamentCatalog.Service/Services/SyncStateService.cs` | service (singleton) | request-response | No existing singleton service for polling state; new pattern per RESEARCH.md |
| `FilamentCatalog.Service/Services/SyncBackgroundService.cs` | background service | request-response | No existing BackgroundService; new pattern per RESEARCH.md + CLAUDE.md |
| `CatalogController.cs` (implied) | controller | request-response | No existing catalog/product API; new controller alongside existing OwnersController/SpoolsController |

**Action:** Use RESEARCH.md patterns directly for these files. Planner should reference:
- SyncStateService: RESEARCH.md §Pattern 4 (lines 407-460)
- SyncBackgroundService: RESEARCH.md §Pattern 3 (lines 314-393)
- CatalogController: RESEARCH.md §Architecture Patterns (lines 121-124) — implement GET /api/catalog/count, /materials, /colors?material=

---

## Metadata

**Analog search scope:** 
- Backend: `FilamentCatalog.Service/Controllers/`, `FilamentCatalog.Service/Services/`, `FilamentCatalog.EntityFramework/Models/`
- Frontend: `FilamentCatalog.Service/wwwroot/js/`, `FilamentCatalog.Service/wwwroot/css/`, `FilamentCatalog.Service/wwwroot/index.html`

**Files scanned:** 30+ (controllers, services, models, views, DTOs)

**Pattern extraction date:** 2026-05-02

**Key alignment notes:**
- All new services follow `IXxxService` + `XxxService` pattern from existing codebase
- All controllers use `[ApiController]` + exception handling from OwnersController template
- All JavaScript follows ES module + event-driven pattern from existing spools.js / owners.js
- All CSS uses design tokens from app.css (--color-accent, --space-md, etc.)
- All database operations use EF Core pattern with `DateTime.UtcNow` and `await db.SaveChangesAsync()`
