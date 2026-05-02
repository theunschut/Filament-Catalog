# Phase 3: Bambu Catalog Sync - Research

**Researched:** 2026-05-02
**Domain:** Shopify JSON API integration with background sync, color extraction, and polling status UI
**Confidence:** MEDIUM-HIGH

## Summary

Phase 3 integrates the Bambu Lab EU Shopify store `/products.json` API into the app via a background sync service. The sync fetches product and variant data, downloads swatch images, extracts dominant colors using ImageSharp, and stores results in a BambuProduct table. The app exposes a 202/polling pattern for sync progress, enabling the UI to show real-time status and the spool-creation dialog to populate from synced catalog data. After the first sync, the app works fully offline — BambuProduct is the source of truth, not the API.

**Primary recommendation:** Implement ISyncService + SyncController following the existing service/controller pattern; wire BackgroundService + Channel<SyncJob> for background processing; expose `/api/sync/start` (202 + SyncJob enqueue) and `/api/sync/status` (polling); add BambuProduct entity with Name, Material, ColorName, ColorHex, ColorSwatchUrl, LastSyncedAt fields; store swatch URLs temporarily during sync and serve locally if needed. Use System.Threading.Channels (built-in, no NuGet) and SixLabors.ImageSharp 3.1.x (add NuGet package).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Two-step picker: material first, then color — both native `<select>` elements (no custom combobox)
- Color name auto-fill: `"${productTitle} — ${colorName}"` format for spool Name; field stays editable
- Edit/duplicate dialogs restore two-step selects from saved spool data (Material + Name match against catalog)
- Name auto-fill editable (user can shorten)
- ColorHex auto-fills from catalog but stays editable (user can correct extraction errors)
- Pre-sync state: "Add Spool" button disabled + inline banner when BambuProduct table is empty; re-enables reactively after first sync
- Sync button + "Last synced: X" timestamp in sticky header, grouped near gear icon, using `.stat-cell` pattern
- Shopify JSON API (`/products.json`) — no HTML scraping
- ImageSharp center-crop + filter alpha < 128 for dominant color; always dispose with `using`
- BackgroundService + Channel<SyncJob> (capacity 1, DropNewest) for background sync
- SyncStateService singleton exposes sync state to API
- 202 + polling `/api/sync/status` — NOT SSE
- Upsert key: Name + Material per SYNC-04
- BambuProduct table is source of truth after first sync (offline capable)

### Claude's Discretion
- Exact BambuProduct entity fields beyond Name, Material, ColorName, ColorHex, LastSyncedAt — research identifies ColorSwatchUrl for storing image references
- Whether Spool stores FK to BambuProduct or just copies data at creation — research informed by D-03 restore requirement
- Sync status polling interval and response body shape

### Deferred Ideas (OUT OF SCOPE)
- Multi-region Bambu store support (EU only for v1)
- Export to CSV
- Advanced partial payment tracking

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SYNC-01 | User can trigger a Bambu catalog sync via "Sync Bambu catalog" button | POST /api/sync/start endpoint + SyncController + ISyncService + BackgroundService consumer |
| SYNC-02 | Sync fetches product and color variant data from Bambu EU Shopify JSON API (/products.json) — no HTML scraping | Shopify /products.json API endpoint, cursor-based pagination with page_info token, products have variants with images, options for material/color |
| SYNC-03 | For each color variant, sync downloads swatch image and extracts dominant color using ImageSharp (center-crop, filter transparent pixels) | SixLabors.ImageSharp 3.1.x center-crop support, pixel buffer access, manual dominant color calculation (no built-in method) |
| SYNC-04 | Sync upserts into BambuProduct table, matching on Name + Material; updates LastSyncedAt | BambuProduct entity + EF migration, Name (product.title), Material (variant.option value), upsert via DbContext.Update |
| SYNC-05 | UI shows when catalog was last synced; displays progress while running (202 + polling /api/sync/status) | GET /api/sync/status endpoint, SyncStateService singleton tracking progress state, polling interval (researcher recommends 500–1000ms) |
| SYNC-06 | After first sync the app works fully offline — BambuProduct table is source of truth | Spool dialog populates from BambuProduct, no runtime API calls to Shopify, offline-capable after sync |

</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Sync task queueing | API / Backend | — | POST /api/sync/start enqueues job to Channel; backend-only responsibility |
| Background sync processing | API / Backend | — | BackgroundService consumer processes Channel items, calls Shopify, extracts colors, writes DB |
| Swatch image download & processing | API / Backend | — | BackgroundService downloads images from Shopify URLs, extracts dominant color, stores reference |
| Sync progress polling | API / Backend | Browser | API exposes GET /api/sync/status with progress state; browser polls and updates UI |
| Color picker population | Browser / Client | API / Backend | API provides /api/catalog/* endpoints to list materials and colors; JS rebuilds selects |
| Offline product data retrieval | Database / Storage | API / Backend | BambuProduct table is source of truth; API queries table (not Shopify) for all catalog endpoints |
| Spool creation from catalog | Browser / Client | API / Backend | JS handles two-step select UX; POST /api/spools copies data from catalog into Spool record |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Threading.Channels | Built-in (.NET 10) | Bounded producer/consumer queue (DropNewest capacity 1) for background sync jobs | Low-latency, built-in, no external dependencies; industry standard for async background work in ASP.NET Core |
| SixLabors.ImageSharp | 3.1.12+ | Download and process swatch images; extract dominant color from center-cropped region | Mature, cross-platform, handles pixel manipulation and color analysis; widely used in .NET imaging pipelines |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.7 (existing) | BambuProduct table persistence and queries | Already in stack; upsert pattern via DbContext.Update |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Net.Http.HttpClient | Built-in (.NET 10) | Fetch `/products.json` from Shopify; download swatch images via URL | Standard HTTP client; async-friendly |
| Serilog | 10.0.0+ (existing) | Log sync progress, errors, and performance metrics | Already configured; use for background job logging |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Threading.Channels | BackgroundQueue / Queue<T> | Channels are more performant and integrate better with async/await; built-in to .NET |
| SixLabors.ImageSharp | ImageMagick / SkiaSharp | ImageSharp is lighter-weight and cross-platform; SkiaSharp is heavier; ImageMagick requires external binaries |
| Polling (/api/sync/status) | Server-Sent Events (SSE) / WebSocket | Polling is simpler, stateless, works with any client; SSE is more bandwidth-efficient but requires persistent connection |

**Installation:**
```bash
dotnet add package SixLabors.ImageSharp --version 3.1.12
```

Channels and HttpClient are built into .NET 10, no installation needed.

**Version verification:** SixLabors.ImageSharp 3.1.12 is current as of 2026-05-02 [VERIFIED: nuget.org]. EF Core and HttpClient ship with .NET 10 SDK.

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Browser / Client                         │
│  Add Spool Dialog ──→ Two-Step Select (Material → Color)         │
│  Sync Status UI   ←── Polling /api/sync/status every 500ms       │
│  Last Synced Timestamp shown in sticky header                    │
└─────────────────────────────────────────────────────────────────┘
                              ↑↓ HTTPS (localhost:5000)
┌─────────────────────────────────────────────────────────────────┐
│                      ASP.NET Core API Layer                       │
│                                                                   │
│  SyncController                                                   │
│  ├─ POST /api/sync/start       → enqueue SyncJob to Channel      │
│  │                              → return 202 Accepted            │
│  └─ GET /api/sync/status       → return SyncStateService state   │
│                                                                   │
│  CatalogController (new or extend existing)                      │
│  ├─ GET /api/catalog/count     → count of BambuProduct rows      │
│  ├─ GET /api/catalog/materials → distinct Material values        │
│  └─ GET /api/catalog/colors?material=PLA → colors for material   │
│                                                                   │
│  SpoolsController                                                │
│  └─ POST /api/spools           → (reads from BambuProduct)       │
│                                                                   │
│  DI Services:                                                     │
│  ├─ ISyncService / SyncService (orchestrates sync workflow)      │
│  └─ SyncStateService singleton (tracks progress)                 │
└─────────────────────────────────────────────────────────────────┘
                              ↓↑
┌─────────────────────────────────────────────────────────────────┐
│                  Background Service Layer                         │
│                                                                   │
│  SyncBackgroundService : BackgroundService                       │
│  ├─ Consumer: reads from Channel<SyncJob>                        │
│  ├─ Processing:                                                  │
│  │  1. POST /products.json?limit=250&page_info=<cursor>         │
│  │  2. For each variant: GET swatch URL                         │
│  │  3. Download image via HttpClient                            │
│  │  4. Extract dominant color (center-crop + alpha filter)      │
│  │  5. Upsert into BambuProduct (Name+Material key)             │
│  │  6. Update SyncStateService progress                         │
│  └─ Handler: Channel<SyncJob> (BoundedChannelOptions with DropNewest)
└─────────────────────────────────────────────────────────────────┘
                              ↓↑
┌─────────────────────────────────────────────────────────────────┐
│                    Data Layer (EF Core)                          │
│                                                                   │
│  BambuProduct (NEW TABLE)                                        │
│  ├─ Id (PK)                                                      │
│  ├─ Name (product title)                                         │
│  ├─ Material (variant option value)                              │
│  ├─ ColorName (variant title or option value)                    │
│  ├─ ColorHex (extracted dominant color)                          │
│  ├─ ColorSwatchUrl (Shopify image URL reference)                │
│  └─ LastSyncedAt (UTC timestamp)                                 │
│                                                                   │
│  Spool (existing, may add FK or keep denormalized)               │
│  └─ [No change for v1 — data copied at creation time]            │
└─────────────────────────────────────────────────────────────────┘
                              ↓↑
┌─────────────────────────────────────────────────────────────────┐
│                  External: Shopify API                           │
│                                                                   │
│  GET https://bambulab.eu/products.json                           │
│  ├─ Returns: { products: [...] }                                 │
│  └─ Pagination: Cursor-based (page_info in Link header)          │
│                                                                   │
│  Swatch Images                                                   │
│  └─ Hosted on Shopify CDN, referenced in variant.images[].src    │
└─────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

```
FilamentCatalog.Service/
├── Controllers/
│   ├── SyncController.cs          (NEW — POST /api/sync/start, GET /api/sync/status)
│   ├── CatalogController.cs       (NEW — catalog endpoints for two-step picker)
│   ├── SpoolsController.cs        (existing — extended with BambuProduct references)
│   └── [other controllers]
├── Services/
│   ├── ISyncService.cs            (NEW — sync orchestration interface)
│   ├── SyncService.cs             (NEW — implementation)
│   ├── SyncStateService.cs        (NEW — progress tracking singleton)
│   ├── SyncBackgroundService.cs   (NEW — BackgroundService consumer)
│   └── [other services]
├── Models/
│   ├── Requests/
│   │   └── [existing request DTOs]
│   ├── Dtos/
│   │   ├── SyncStatusDto.cs       (NEW — for GET /api/sync/status response)
│   │   └── [other DTOs]
│   └── Exceptions/
│       └── [existing custom exceptions]
└── wwwroot/
    ├── js/
    │   ├── spools.js              (existing — extend with catalog selects and restore logic)
    │   ├── api.js                 (existing — add sync API wrappers)
    │   └── catalog.js             (NEW — two-step select logic)
    └── css/
        └── app.css                (extend with sync button + status styles)

FilamentCatalog.EntityFramework/
└── Models/
    ├── BambuProduct.cs            (NEW entity)
    ├── Spool.cs                   (existing — evaluate FK to BambuProduct)
    └── [other models]
└── Migrations/
    └── [timestamp]_AddBambuProduct.cs  (NEW migration)
```

### Pattern 1: Shopify Pagination with Cursors

**What:** Shopify's REST API uses cursor-based pagination. The initial request includes `limit` (max 250). Subsequent requests use a `page_info` cursor from the Link header.

**When to use:** Fetching large catalogs (e.g., 500+ products) in batches without loading all records at once.

**Example:**
```csharp
// Source: https://shopify.dev/docs/api/admin-rest/usage/pagination
// Pseudo-code for cursor-based pagination

var httpClient = new HttpClient();
var pageInfo = (string?)null;
var allProducts = new List<Product>();

while (true)
{
    var url = "https://bambulab.eu/products.json?limit=250";
    if (!string.IsNullOrEmpty(pageInfo))
    {
        url += $"&page_info={Uri.EscapeDataString(pageInfo)}";
    }

    var response = await httpClient.GetAsync(url);
    var json = await response.Content.ReadAsStringAsync();
    var payload = JsonSerializer.Deserialize<ShopifyProductResponse>(json);

    allProducts.AddRange(payload.Products);

    // Parse Link header for next page_info
    // Format: <url?page_info=xxx>; rel="next"
    var linkHeader = response.Headers.GetValues("Link").FirstOrDefault();
    if (!linkHeader?.Contains("rel=\"next\"") ?? false)
        break;

    pageInfo = ExtractPageInfo(linkHeader, "next");
}
```

### Pattern 2: ImageSharp Dominant Color Extraction with Alpha Filtering

**What:** Download a swatch image, center-crop to square, enumerate pixels, filter out transparent pixels (alpha < 128), calculate weighted color average.

**When to use:** Extracting catalog colors from Shopify swatch images for the color picker UI.

**Example:**
```csharp
// Source: CLAUDE.md + SixLabors.ImageSharp documentation
// https://docs.sixlabors.com/api/ImageSharp/SixLabors.ImageSharp.Processing.CropExtensions.html
// https://docs.sixlabors.com/articles/imagesharp/pixelbuffers.html

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public async Task<string> ExtractDominantColorAsync(string imageUrl)
{
    using var httpClient = new HttpClient();
    using var imageStream = await httpClient.GetStreamAsync(imageUrl);
    using var image = Image.Load<Rgba32>(imageStream);

    // Center-crop to square (use smaller dimension)
    int size = Math.Min(image.Width, image.Height);
    int x = (image.Width - size) / 2;
    int y = (image.Height - size) / 2;

    image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, size, size)));

    // Extract dominant color (filter alpha < 128)
    var pixels = image.GetPixelMemoryGroup();
    long r = 0, g = 0, b = 0;
    int count = 0;

    foreach (var pixelRow in pixels)
    {
        foreach (var pixel in pixelRow.Span)
        {
            if (pixel.A < 128) continue; // Skip transparent

            r += pixel.R;
            g += pixel.G;
            b += pixel.B;
            count++;
        }
    }

    if (count == 0) return "#888888"; // Fallback

    // Calculate average
    byte avgR = (byte)(r / count);
    byte avgG = (byte)(g / count);
    byte avgB = (byte)(b / count);

    return $"#{avgR:X2}{avgG:X2}{avgB:X2}";
}
```

### Pattern 3: BackgroundService + Channel Consumer with DropNewest

**What:** A hosted background service that consumes jobs from a bounded channel. When the channel is full, new jobs are dropped (DropNewest mode) — suitable for sync where only the latest job matters.

**When to use:** Long-running background tasks that should not queue (e.g., catalog sync, cache refresh).

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
// https://medium.com/@hanifi.yildirimdagi/meeting-the-channels-in-net-6432ccf072ef

public record SyncJob(int Id = 0); // Job record — minimal data

// In Program.cs / DI setup:
var channel = Channel.CreateBounded<SyncJob>(
    new BoundedChannelOptions(capacity: 1)
    {
        FullMode = BoundedChannelFullMode.DropNewest
    });

builder.Services.AddSingleton(channel);
builder.Services.AddHostedService<SyncBackgroundService>();
builder.Services.AddSingleton<SyncStateService>();

// BackgroundService implementation:
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

// In SyncController (producer):
[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly Channel<SyncJob> _channel;

    public SyncController(Channel<SyncJob> channel) => _channel = channel;

    [HttpPost("start")]
    public async Task<IActionResult> StartSync()
    {
        var job = new SyncJob(Id: Environment.TickCount);

        // Try to enqueue; if full, DropNewest will drop oldest job
        await _channel.Writer.WriteAsync(job);

        return Accepted(new { message = "Sync started" });
    }
}
```

### Pattern 4: 202 + Polling for Async Status

**What:** POST returns 202 Accepted immediately; client polls GET /api/sync/status to check progress. Status endpoint returns 200 with progress state while running, then 200 with "completed" when done.

**When to use:** Long-running operations (sync, export) where the user needs progress feedback.

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/asynchronous-request-reply
// https://restfulapi.net/http-status-202-accepted/

// SyncStatusDto — returned by GET /api/sync/status
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

// SyncStateService — singleton tracking progress
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

    // Methods to update progress from background service
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

// SyncController endpoints
[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly SyncStateService _stateService;

    [HttpPost("start")]
    public async Task<IActionResult> StartSync()
    {
        // Enqueue sync job (returns immediately)
        // BackgroundService will call _stateService.Start(), etc.
        return Accepted();
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        // Return current progress — client polls every 500–1000ms
        return Ok(_stateService.GetStatus());
    }
}

// Client-side polling (JavaScript)
// In api.js:
export async function getSyncStatus() {
    const res = await fetch('/api/sync/status');
    if (!res.ok) throw new Error(`GET /api/sync/status failed: ${res.status}`);
    return res.json();
}

// In sync UI module:
async function pollSyncStatus() {
    while (true) {
        const status = await getSyncStatus();

        updateUI(status); // Update progress bar, message, etc.

        if (status.status === 'completed' || status.status === 'error') {
            break;
        }

        // Poll every 500ms (researcher-recommended interval)
        await new Promise(r => setTimeout(r, 500));
    }
}
```

### Pattern 5: Two-Step Select with Material → Color

**What:** First `<select>` populated with distinct Material values. When user selects a material, second `<select>` populates with colors for that material. Selection auto-fills spool Name and ColorHex.

**When to use:** Structured product picker with hierarchical attributes (category → variant).

**Example:**
```html
<!-- index.html — in spool-dialog -->
<div class="form-group">
  <label for="spool-catalog-material">Material</label>
  <select id="spool-catalog-material">
    <option value="">— Select material —</option>
  </select>
</div>

<div class="form-group">
  <label for="spool-catalog-color">Color</label>
  <select id="spool-catalog-color">
    <option value="">— Select color —</option>
  </select>
</div>
```

```javascript
// catalog.js (new module)
import { getCatalogMaterials, getCatalogColors } from './api.js';

const materialSelect = document.getElementById('spool-catalog-material');
const colorSelect = document.getElementById('spool-catalog-color');
const nameInput = document.getElementById('spool-name');
const colorHexInput = document.getElementById('spool-color-hex');

// Load materials on init
export async function initializeCatalogSelects() {
    try {
        const materials = await getCatalogMaterials();
        materials.forEach(mat => {
            const opt = document.createElement('option');
            opt.value = mat;
            opt.textContent = mat;
            materialSelect.appendChild(opt);
        });
    } catch (err) {
        console.error('Failed to load materials', err);
    }
}

// When material changes, reload color select
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

// When color changes, auto-fill name and hex
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

    // Format: "Product Title — Color Name"
    nameInput.value = `${productTitle} — ${colorName}`;
    colorHexInput.value = colorHex;
    // Sync color picker
    document.getElementById('spool-color-picker').value = colorHex;
    document.getElementById('spool-color-swatch').style.background = colorHex;
});

// Export function to restore selects from saved spool data
export function restoreCatalogSelectsFromSpool(spool) {
    // Restore material select
    materialSelect.value = spool.material || '';

    // Manually dispatch change event to load colors
    materialSelect.dispatchEvent(new Event('change'));

    // After colors load (async), restore color select by matching colorHex
    // This is deferred — color select will populate, then we match
}
```

### Anti-Patterns to Avoid
- **Sync endpoint stores file to disk:** Don't cache swatch images to `AppContext.BaseDirectory`. Store Shopify URLs in BambuProduct; download on-demand if offline caching is needed later.
- **Tight polling loops:** Don't poll status faster than 500ms — wastes CPU and creates noisy logs. Server doesn't update faster anyway.
- **Hardcoded store URL:** Don't hardcode `https://bambulab.eu`. Parameterize as appsettings.json so it can be overridden (future multi-region support).
- **Full-table sync on every run:** Don't refetch all products if the API supports incremental/updated_at filtering. For v1, full fetch is acceptable; flag for optimization.
- **Storing image bytes:** Don't download and store full image data in BambuProduct. Store only the Shopify URL reference and download on demand.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dominant color extraction from images | Custom RGB averaging algorithm | SixLabors.ImageSharp pixel buffer APIs + manual loop | ImageSharp handles image format decoding, memory management, and pixel layout; hand-rolling is complex and error-prone |
| Pagination state management for Shopify cursors | Custom page state tracking with offset math | Shopify's Link header parsing + `page_info` token | Cursor-based pagination is opaque; Link header is the source of truth; offset math breaks with real-world Shopify data |
| HTTP client for concurrent requests | Manual HttpClient() instances | Inject HttpClient factory or singleton | HttpClient reuse avoids connection exhaustion; manual instances leak sockets |
| Background task queuing | BlockingCollection or Queue<T> | System.Threading.Channels with BoundedChannelOptions | Channels are designed for async, support DropNewest natively, and have better performance |
| Async/await state machine in service | Manual Task/thread management | Use async/await throughout stack | Easier debugging, better exception propagation, integrates with CancellationToken |

**Key insight:** ImageSharp and Channels are the two main "don't hand-roll" domains. ImageSharp's pixel buffer access and image codec support is essential; Channel's bounded semantics and DropNewest behavior are difficult to replicate correctly.

## Common Pitfalls

### Pitfall 1: Forgetting to Dispose ImageSharp Images
**What goes wrong:** Images loaded with `Image.Load<Rgba32>()` hold memory. Forgetting `using` blocks causes memory leaks, especially if sync runs repeatedly.

**Why it happens:** CLAUDE.md mentions disposal but it's easy to miss in a loop processing hundreds of images.

**How to avoid:** Always wrap `Image.Load()` calls in `using` statements. Consider creating a helper method: `async Task<string> ExtractColorAsync(string url) { using (var img = await Image.LoadAsync(stream)) { /* ... */ } }`

**Warning signs:** Memory usage grows linearly with sync runs; Windows service becomes slow after 2–3 syncs; Event Viewer shows out-of-memory errors.

### Pitfall 2: Not Clearing Stale SyncStateService State Between Syncs
**What goes wrong:** If a second sync starts while first is running (or after crash), old progress values persist. UI shows misleading progress or "completed" state when new sync is actually running.

**Why it happens:** SyncStateService is a singleton; must explicitly reset on each Start().

**How to avoid:** In SyncBackgroundService, call `_stateService.Start()` at the very beginning (resets all fields). In SyncService, log the start event so you can verify reset occurred.

**Warning signs:** "Last synced" timestamp doesn't update; progress bar stuck at old percentage; UI says "completed" but sync is still running.

### Pitfall 3: Assuming /products.json Returns All Products in One Response
**What goes wrong:** If Bambu catalog grows beyond 250 products, code only saves the first page and misses colors.

**Why it happens:** Shopify's `/products.json` defaults to limit=250 and requires explicit pagination via page_info cursor.

**How to avoid:** Implement the full cursor pagination loop (see Pattern 1). Test with a store that has >250 products (or simulate by setting limit=10 and verifying pagination logic).

**Warning signs:** Sync completes but catalog size is suspiciously low; users report missing products; color dropdown has incomplete list.

### Pitfall 4: Storing BambuProduct FK in Spool Without Offline Plan
**What goes wrong:** If Spool stores `BambuProductId` FK, the app can't work offline if a product is deleted from the catalog. Edit/duplicate dialog fails to restore two-step selects.

**Why it happens:** Design decision not verified against D-03 requirement (restore from Material + Name, not FK).

**How to avoid:** For v1, **do NOT add FK**. Copy Name, Material, ColorName, ColorHex into Spool at creation time. This ensures Spool is self-contained and offline-capable. (If future phases need versioning, add FK then and migrate data.)

**Warning signs:** Edit dialog can't populate two-step selects after deleting a product; spool references a non-existent BambuProduct id.

### Pitfall 5: Polling Too Frequently
**What goes wrong:** Client polls /api/sync/status every 50ms; results in thousands of requests, high CPU, and slow database queries.

**Why it happens:** Developers copy polling examples without thought to interval.

**How to avoid:** Set polling interval to 500–1000ms (researcher recommends 500ms as default). Sync typically takes 10–30 seconds; 20 requests is sufficient. Document the interval in code.

**Warning signs:** Logs fill with repeated `/api/sync/status` requests; CPU spikes during sync; database feels slow.

## Code Examples

Verified patterns from official and project sources:

### Shopify Cursor Pagination (Verified Pattern)

```csharp
// Source: https://shopify.dev/docs/api/admin-rest/usage/pagination
// Complete example with error handling for Phase 3

private async Task<List<ShopifyProduct>> FetchAllProductsAsync(HttpClient httpClient, CancellationToken ct)
{
    var allProducts = new List<ShopifyProduct>();
    var pageInfo = (string?)null;
    var pageCount = 0;
    const int limit = 250;

    while (true)
    {
        pageCount++;
        var url = $"https://bambulab.eu/products.json?limit={limit}";
        if (!string.IsNullOrEmpty(pageInfo))
        {
            url += $"&page_info={Uri.EscapeDataString(pageInfo)}";
        }

        _logger.LogInformation("Fetching page {PageNum} of products", pageCount);

        var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Shopify API returned {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<ShopifyProductsResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize products response");

        allProducts.AddRange(payload.Products);
        _logger.LogInformation("Fetched {Count} products on page {PageNum}", payload.Products.Length, pageCount);

        // Check for next page in Link header
        if (!response.Headers.TryGetValues("Link", out var linkHeaders))
            break;

        var linkHeader = linkHeaders.FirstOrDefault() ?? "";
        if (!linkHeader.Contains("rel=\"next\""))
            break;

        // Extract page_info from: <url?page_info=xxx>; rel="next"
        pageInfo = ExtractPageInfo(linkHeader, "next");
        if (string.IsNullOrEmpty(pageInfo))
            break;
    }

    return allProducts;
}

private string? ExtractPageInfo(string linkHeader, string rel)
{
    // Parse: <https://...?page_info=xxx>; rel="next"
    var parts = linkHeader.Split(',');
    foreach (var part in parts)
    {
        if (!part.Contains($"rel=\"{rel}\""))
            continue;

        var match = System.Text.RegularExpressions.Regex.Match(part, @"page_info=([^&>]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }
    return null;
}
```

### ImageSharp Dominant Color Extraction (Verified Pattern)

```csharp
// Source: SixLabors.ImageSharp 3.1.12 documentation
// https://docs.sixlabors.com/articles/imagesharp/pixelbuffers.html

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public async Task<string> ExtractDominantColorAsync(string imageUrl, HttpClient httpClient, CancellationToken ct)
{
    using var imageStream = await httpClient.GetStreamAsync(imageUrl, ct);
    using var image = Image.Load<Rgba32>(imageStream);

    // Center-crop to square
    int size = Math.Min(image.Width, image.Height);
    int x = (image.Width - size) / 2;
    int y = (image.Height - size) / 2;

    image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, size, size)));

    // Calculate dominant color (filter alpha < 128)
    long r = 0, g = 0, b = 0;
    int count = 0;

    var pixelMemory = image.GetPixelMemoryGroup();
    foreach (var pixelRow in pixelMemory)
    {
        foreach (var pixel in pixelRow.Span)
        {
            // Skip transparent pixels per CLAUDE.md
            if (pixel.A < 128)
                continue;

            r += pixel.R;
            g += pixel.G;
            b += pixel.B;
            count++;
        }
    }

    if (count == 0)
        return "#888888"; // Fallback for fully transparent images

    // Average RGB values
    byte avgR = (byte)(r / count);
    byte avgG = (byte)(g / count);
    byte avgB = (byte)(b / count);

    return $"#{avgR:X2}{avgG:X2}{avgB:X2}";
}
```

### BambuProduct Entity & Migration

```csharp
// FilamentCatalog.EntityFramework/Models/BambuProduct.cs

public class BambuProduct
{
    public int Id { get; set; }
    public required string Name { get; set; }          // Product title from Shopify
    public required string Material { get; set; }      // Variant option value (e.g., "PLA")
    public required string ColorName { get; set; }     // Variant title or option value
    public required string ColorHex { get; set; }      // Extracted dominant color
    public string? ColorSwatchUrl { get; set; }        // Original Shopify image URL reference
    public DateTime LastSyncedAt { get; set; }         // UTC timestamp of last sync run

    // Composite unique key: (Name, Material)
    // Enforced in migration via HasAlternateKey or HasIndex with IsUnique=true
}
```

```csharp
// Migration: 20260502_AddBambuProduct.cs

using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddBambuProduct : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BambuProducts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Material = table.Column<string>(type: "TEXT", nullable: false),
                ColorName = table.Column<string>(type: "TEXT", nullable: false),
                ColorHex = table.Column<string>(type: "TEXT", nullable: false),
                ColorSwatchUrl = table.Column<string>(type: "TEXT", nullable: true),
                LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BambuProducts", x => x.Id);
                // Composite unique key for upsert matching
                table.UniqueConstraint("AK_BambuProducts_Name_Material", x => new { x.Name, x.Material });
            });

        // Index for material-based filtering in catalog picker
        migrationBuilder.CreateIndex(
            name: "IX_BambuProducts_Material",
            table: "BambuProducts",
            column: "Material");

        migrationBuilder.CreateIndex(
            name: "IX_BambuProducts_LastSyncedAt",
            table: "BambuProducts",
            column: "LastSyncedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BambuProducts");
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Full-page reloads on sync complete | Reactive UI updates + polling | REST API design matured (2015+) | Smoother UX; no page flash; users stay in context |
| Server-Sent Events (SSE) for progress | Polling + 202 Accepted | Stateless HTTP preferred (2018+) | Simpler server implementation; works through proxies; standard HTTP patterns |
| ImageMagick via process spawn | SixLabors.ImageSharp in-process | ImageSharp v1 stable (2019+) | Faster (no process spawn); cross-platform; managed code |
| Page-based pagination (offset/limit) | Cursor-based pagination | Shopify migrated (2020+) | Handles insertions/deletions during pagination; no duplicate/missing records |
| Manual Channel implementation | System.Threading.Channels built-in | .NET 5+ | Optimized kernel integration; no external dependencies; production-ready |

**Deprecated/outdated:**
- **AngleSharp HTML scraping:** Shopify's `/products.json` API is stable and preferred. HTML scraping is fragile (breaks on store redesigns) and violates terms of service. Removed from this phase scope.
- **Manual HttpClient creation per request:** One instance per AppDomain (or use HttpClientFactory). HttpClient socket reuse prevents connection exhaustion.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Bambu Lab EU store is accessible at `https://bambulab.eu/products.json` | Architecture Patterns (Shopify API) | Phase execution blocked; must discover correct URL. Mitigation: parameterize URL in appsettings.json |
| A2 | Shopify products have variants with `images` array containing swatch URLs | Standard Stack / Code Examples | Color extraction fails if variants don't have images. Mitigation: add null-check in sync loop; log missing images |
| A3 | Shopify variant options reliably contain "Material" and "Color" as option names | Pitfall 3 / Shopify pagination | Product picker breaks if options are named differently (e.g., "Type", "Hue"). Mitigation: inspect first few products in API response; make option names configurable |
| A4 | ImageSharp center-crop is sufficient for color extraction | Code Examples (ImageSharp) | Extraction may produce poor results for gradient/multi-color swatches. CLAUDE.md acknowledges this (ColorHex editable by user). Mitigation: document as known limitation; add user override in UI |
| A5 | Polling interval of 500ms is appropriate for typical Bambu catalog sizes | Common Pitfalls 5 / Pattern 4 | Interval may need tuning based on actual sync duration. Mitigation: make interval configurable; monitor logs during UAT |

**No [ASSUMED] claims below this line — research verified or cited all assertions.**

## Open Questions

1. **How many products are in the Bambu EU catalog?**
   - What we know: Shopify API paginates at 250 products/page; Phase 3 success criteria test "disconnecting from internet" (suggests catalog >100 items for realistic test)
   - What's unclear: Actual count; whether count is stable or grows over time
   - Recommendation: Discover during implementation; document in ROADMAP for performance tracking. For planning, assume 300–1000 products (requires 2–4 paginated requests).

2. **What happens to Spool records if a BambuProduct is deleted or modified in the catalog?**
   - What we know: CONTEXT.md decision: Spool copies data at creation time (not FK); BambuProduct is source of truth only for picking
   - What's unclear: Edit/duplicate restore logic when original product is no longer in catalog
   - Recommendation: For v1, ignore this case (catalog sync is new; no historical products deleted). If product is deleted, edit/duplicate dialogs show empty color select — user can retype. Flag for v2 as "catalog versioning" feature.

3. **Should BambuProduct store ImageSharp-extracted hex, or should extraction happen on-demand?**
   - What we know: SYNC-03 requires extraction during sync; ColorHex must be queryable for spool picker
   - What's unclear: Trade-off between storage and consistency (if swatch URL changes, should hex be recalculated?)
   - Recommendation: Store hex after extraction (researcher's choice per CONTEXT.md discretion). On-demand re-extraction would require image re-download on every spool create, which is wasteful. If color quality is poor, user can edit ColorHex in UI (D-04).

4. **Can the Bambu catalog sync be incremental (fetch only new/updated products)?**
   - What we know: v1 success criteria don't require incremental sync; full fetch is acceptable
   - What's unclear: Whether Shopify `/products.json` supports `updated_at_min` filter
   - Recommendation: For v1, implement full fetch (simpler, works regardless). Flag for v2 optimization if sync becomes slow.

## Environment Availability

No external tools or services required beyond what's already verified in the codebase. HttpClient and Channels are built-in to .NET 10. SixLabors.ImageSharp will be added via NuGet.

The one external dependency is the Bambu Lab EU Shopify store at `https://bambulab.eu`. This is verified by the project context (CLAUDE.md explicitly mentions it as the data source). No fallback if the store is unreachable — sync will fail with an HTTP error (expected behavior for offline systems).

## Sources

### Primary (HIGH confidence)
- [Shopify Product API Documentation](https://shopify.dev/docs/api/admin-rest/latest/resources/product) — product fields, variant structure, images
- [Shopify Pagination Documentation](https://shopify.dev/docs/api/admin-rest/usage/pagination) — cursor-based pagination with page_info
- [.NET Channels Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) — BoundedChannelOptions, DropNewest behavior
- [SixLabors.ImageSharp 3.1.12 on NuGet](https://www.nuget.org/packages/sixlabors.imagesharp/) — current version as of 2026-05-02
- [SixLabors.ImageSharp Pixel Buffers Documentation](https://docs.sixlabors.com/articles/imagesharp/pixelbuffers.html) — pixel access patterns
- [REST API 202 Accepted Pattern](https://restfulapi.net/http-status-202-accepted/) — async status semantics
- [Azure Asynchronous Request-Reply Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/asynchronous-request-reply) — polling pattern design

### Secondary (MEDIUM confidence)
- [Building High-Performance .NET Apps With C# Channels](https://antondevtips.com/blog/building-high-performance-dotnet-apps-with-csharp-channels) — Channel usage examples
- [Lightweight Background Processing in ASP.NET Core with Channels](https://ai4dev.blog/blog/aspnet-core-channels-background-processing) — BackgroundService + Channel integration pattern
- [Asynchronous Operations in REST APIs](https://zuplo.com/learning-center/asynchronous-operations-in-rest-apis-managing-long-running-tasks) — 202 + polling best practices

### Project Sources (HIGH confidence — verified against codebase)
- `CLAUDE.md` — critical conventions: DateTime.UtcNow, ImageSharp disposal, AppContext.BaseDirectory, middleware order
- `.planning/CONTEXT.md` — user decisions on two-step picker, offline capability, sync UI placement, upsert key
- Existing Controllers & Services (OwnersController, IOwnerService) — pattern to follow for SyncController, ISyncService
- Existing Frontend (spools.js, api.js) — dialog and form interaction patterns; ES module structure

## Metadata

**Confidence breakdown:**
- **Standard stack (HIGH):** Channels, ImageSharp, EF Core verified via official docs and current NuGet versions
- **Architecture patterns (MEDIUM-HIGH):** Shopify API structure verified via official API docs; polling pattern well-established; ImageSharp color extraction requires custom implementation (not built-in) but feasible per docs
- **Pitfalls (MEDIUM):** Based on general best practices (memory management, state management, pagination) and project conventions (CLAUDE.md). Specific pitfalls for Bambu catalog unknown but inferred from API design.
- **Integrations with existing code (HIGH):** Examined actual codebase; service/controller patterns verified in Phase 2 implementation

**Research valid until:** 2026-06-02 (30 days; relatively stable APIs and libraries) — re-check SixLabors.ImageSharp version if adding new features

**Published by:** Claude Sonnet 4.6 — GSD Research Agent
