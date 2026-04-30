# Architecture Research

**Project:** Filament Catalog
**Researched:** 2026-04-30
**Overall confidence:** HIGH (official docs + verified .NET 10 sources)

---

## Component Overview

The app is a single .NET 10 process hosting all concerns together. There is no reason to split this across projects — the complexity does not warrant it.

```
FilamentCatalog.csproj
│
├── Program.cs                    ← app composition root, middleware, migrations
│
├── Data/
│   ├── AppDbContext.cs           ← EF Core context
│   └── Migrations/               ← EF-generated migration files
│
├── Entities/
│   ├── Owner.cs
│   ├── BambuProduct.cs
│   └── Spool.cs
│
├── Endpoints/
│   ├── SpoolEndpoints.cs         ← MapGroup("/api/spools")
│   ├── OwnerEndpoints.cs         ← MapGroup("/api/owners")
│   ├── ProductEndpoints.cs       ← MapGroup("/api/products")
│   ├── SummaryEndpoints.cs       ← /api/summary, /api/balance
│   └── SyncEndpoints.cs          ← POST /api/sync, GET /api/sync/status, GET /api/sync/stream
│
├── Services/
│   ├── SyncStateService.cs       ← singleton; holds running flag + progress
│   └── BambuScraperService.cs    ← the scraping + color extraction logic
│
├── Workers/
│   └── SyncWorker.cs             ← BackgroundService; dequeues and runs scrape jobs
│
└── wwwroot/
    └── index.html                ← entire frontend
```

**Request flow:**

1. Browser calls `POST /api/sync` → endpoint writes trigger to `Channel<SyncJob>` → returns 202 immediately
2. `SyncWorker.ExecuteAsync` is looping on `Channel.Reader.ReadAsync` → picks up the job
3. `SyncWorker` creates a DI scope, resolves `BambuScraperService`, runs scrape
4. Progress is written to `SyncStateService` (singleton, thread-safe) as scraping proceeds
5. Browser polls `GET /api/sync/status` every 2 seconds OR holds open `GET /api/sync/stream` SSE connection
6. `SyncStateService` is read by both the status endpoint and the SSE stream

---

## Background Sync Architecture

### Recommended Pattern: BackgroundService + Channel<T> + Singleton State

**Why not a raw `IHostedService`:** `BackgroundService` is the correct base class. It implements `IHostedService` and provides the `ExecuteAsync(CancellationToken)` hook. You do not need to implement `StartAsync`/`StopAsync` manually. The official .NET 10 docs confirm this is the standard approach.

**Why `Channel<T>` not `Task.Run` from the endpoint:** The API endpoint must return quickly. Launching `Task.Run` from inside a request handler is fire-and-forget with no cancellation and no backpressure. A `Channel<SyncJob>` decouples trigger from execution cleanly, and `BoundedChannelFullMode.DropNewest` (capacity 1) means a second "sync" click while one is running simply does nothing — the channel is full.

**Why a singleton `SyncStateService`:** The scraper is a background job (singleton lifetime via `SyncWorker`). Progress data needs to be readable from HTTP request handlers (scoped lifetime). A singleton bridge is the standard .NET pattern for crossing that boundary. It holds: `IsRunning`, `ProductsScraped`, `TotalProducts`, `LastSyncedAt`, `LastError`. All writes from the worker, reads from endpoints.

**Concrete structure:**

```csharp
// SyncStateService.cs — singleton
public class SyncStateService
{
    private int _productsScraped;
    private int _totalProducts;

    public bool IsRunning { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public int ProductsScraped => _productsScraped;
    public int TotalProducts => _totalProducts;
    public string? LastError { get; private set; }

    public void StartSync(int total)
    {
        IsRunning = true;
        _productsScraped = 0;
        _totalProducts = total;
        LastError = null;
    }

    public void ReportProgress() => Interlocked.Increment(ref _productsScraped);

    public void CompleteSync(DateTime syncedAt)
    {
        IsRunning = false;
        LastSyncedAt = syncedAt;
    }

    public void FailSync(string error)
    {
        IsRunning = false;
        LastError = error;
    }
}

// SyncWorker.cs — BackgroundService
public class SyncWorker(
    Channel<SyncJob> channel,
    IServiceScopeFactory scopeFactory,
    SyncStateService state,
    ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in channel.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var scraper = scope.ServiceProvider.GetRequiredService<BambuScraperService>();
            try
            {
                await scraper.RunAsync(state, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                state.FailSync(ex.Message);
                logger.LogError(ex, "Sync failed");
            }
        }
    }
}
```

Register in `Program.cs`:
```csharp
var syncChannel = Channel.CreateBounded<SyncJob>(new BoundedChannelOptions(1)
{
    FullMode = BoundedChannelFullMode.DropNewest
});
builder.Services.AddSingleton(syncChannel);
builder.Services.AddSingleton<SyncStateService>();
builder.Services.AddScoped<BambuScraperService>();
builder.Services.AddHostedService<SyncWorker>();
```

**Scoped services in BackgroundService:** `BambuScraperService` needs `AppDbContext` (scoped). The worker must create a DI scope per job with `IServiceScopeFactory.CreateScope()`. This is the official pattern from Microsoft docs — BackgroundService is singleton-lifetime, so it cannot directly inject scoped services.

---

## SSE vs Polling Decision: Use Polling

**Recommendation: polling (`GET /api/sync/status` every 2 seconds)**

**Rationale:**

- `.NET 10` does have a native `TypedResults.ServerSentEvents(IAsyncEnumerable<T>)` API (confirmed via official sources, released with .NET 10). It is genuinely simple to implement in minimal API.
- However, SSE introduces a persistent open connection per browser tab. For a single-user local app this is not a scaling concern, but it adds complexity that polling does not have:
  - The SSE endpoint needs to `await` on `SyncStateService` changes, which requires either a `Channel` or `SemaphoreSlim`/`TaskCompletionSource` mechanism to avoid busy-polling on the server side.
  - Client reconnection after browser navigate or tab focus regain must be handled.
  - Windows service + Kestrel + SSE works fine — there is no IIS buffering issue since Kestrel writes directly. But the added wiring is not justified here.
- Polling at 2-second intervals for a sync job that takes 30–120 seconds is perfectly adequate UX. The status endpoint is a trivial JSON read from the singleton.

**If SSE is desired later:** The .NET 10 pattern is:
```csharp
app.MapGet("/api/sync/stream", (SyncStateService state, CancellationToken ct) =>
    TypedResults.ServerSentEvents(state.GetProgressStream(ct), eventType: "progress"));
```
Where `GetProgressStream` returns an `IAsyncEnumerable<SyncStatusDto>` that yields on each state change. The `CancellationToken` handles client disconnect automatically.

**Windows service gotcha with SSE:** None specific to the hosting model. `UseWindowsService()` runs Kestrel as normal; the Windows Service wrapper only changes how the process lifecycle is managed (start/stop signals). SSE connections are handled identically to console hosting.

---

## Project Structure

**Recommendation: single `.csproj`, feature-grouped files**

This is a small app (~15 source files, ~3 entity types, ~10 endpoints). Do not use separate projects for "Data", "Services", etc. — the layer overhead exceeds the benefit at this size. Single project with folders by responsibility.

**Endpoint organization:** Use static extension classes that call `MapGroup`, registered from `Program.cs`. This is the idiomatic minimal API approach without needing Carter or other libraries.

```csharp
// Endpoints/SpoolEndpoints.cs
public static class SpoolEndpoints
{
    public static void MapSpoolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/spools");
        group.MapGet("/", ListSpools);
        group.MapPost("/", CreateSpool);
        group.MapPut("/{id}", UpdateSpool);
        group.MapDelete("/{id}", DeleteSpool);
    }

    private static async Task<IResult> ListSpools(
        AppDbContext db,
        [AsParameters] SpoolFilters filters) { ... }
    // ...
}

// Program.cs
app.MapSpoolEndpoints();
app.MapOwnerEndpoints();
app.MapProductEndpoints();
app.MapSummaryEndpoints();
app.MapSyncEndpoints();
```

**Recommended full layout:**

```
FilamentCatalog/
├── FilamentCatalog.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
│
├── Data/
│   ├── AppDbContext.cs
│   └── Migrations/
│       └── (generated)
│
├── Entities/
│   ├── Owner.cs
│   ├── BambuProduct.cs
│   └── Spool.cs
│
├── Endpoints/
│   ├── SpoolEndpoints.cs
│   ├── OwnerEndpoints.cs
│   ├── ProductEndpoints.cs
│   ├── SummaryEndpoints.cs
│   └── SyncEndpoints.cs
│
├── Services/
│   ├── SyncStateService.cs
│   └── BambuScraperService.cs
│
├── Workers/
│   └── SyncWorker.cs
│
└── wwwroot/
    └── index.html
```

---

## Frontend Organization

**Single `index.html` with inline `<style>` and `<script>` blocks, or one companion `.css` and `.js` file served from `wwwroot/`.**

The app has distinct UI sections that map cleanly to a module pattern without any framework:

```
index.html
wwwroot/
├── app.js          ← main module; init, event wiring, state
├── api.js          ← all fetch() calls, one function per endpoint
├── spools.js       ← spool table render + filter logic
├── modals.js       ← open/close modal helpers + form logic
├── owners.js       ← owners list management
├── balance.js      ← balance section render
└── style.css       ← all styles
```

**Module loading via native ES modules** (`<script type="module">`). No bundler, no build step. Browsers supporting ES modules also support `import`/`export`, which is universal for a local Windows 10/11 app.

```html
<!-- index.html -->
<script type="module" src="/app.js"></script>
```

```js
// api.js — all network calls in one place
export async function getSpools(filters) {
    const params = new URLSearchParams(filters);
    const res = await fetch(`/api/spools?${params}`);
    if (!res.ok) throw new Error(await res.text());
    return res.json();
}

export async function createSpool(data) {
    const res = await fetch('/api/spools', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
}
// ... etc
```

```js
// app.js — wiring
import { getSpools } from './api.js';
import { renderSpoolTable } from './spools.js';
import { openAddSpoolModal } from './modals.js';

document.getElementById('btn-add-spool').addEventListener('click', openAddSpoolModal);
// ...
```

**Modal pattern:** Use a single `<dialog>` element per modal form (native HTML `<dialog>` has good browser support and handles focus trap and Escape key). One `<dialog id="modal-add-spool">` for add/edit (reuse the same form, populate for edit). One `<dialog id="modal-owners">` for owner management.

```js
// modals.js
export function openAddSpoolModal(existingSpool = null) {
    const dialog = document.getElementById('modal-add-spool');
    const form = dialog.querySelector('form');
    form.reset();
    if (existingSpool) populateForm(form, existingSpool);
    dialog.showModal();
}

export function closeAddSpoolModal() {
    document.getElementById('modal-add-spool').close();
}
```

**State pattern for the table:** Keep current filter state as a plain object. Re-render the table completely on each filter change (no virtual DOM needed for ~100 rows):

```js
// spools.js
let currentFilters = { owner: '', status: '', payment: '', q: '' };

export function applyFilter(key, value) {
    currentFilters[key] = value;
    refreshSpoolTable();
}

export async function refreshSpoolTable() {
    const spools = await getSpools(currentFilters);
    renderSpoolTable(spools);
}
```

**Color swatch rendering:** Use an inline `<span>` with `background-color: #hex` styled as a small circle. No image fetch needed — the hex is stored in `BambuProduct.ColorHex`.

**Sync polling:** In `app.js`, start a `setInterval` when sync is triggered, poll `/api/sync/status` every 2s, update a status element, stop the interval when `isRunning` is false.

---

## Data Layer Patterns

### EF Core Setup

`AppDbContext` is straightforward. Use `string` enum-like fields (not C# enums) for `PaymentStatus` and `SpoolStatus` since the spec defines them as string values and SQLite stores them as text anyway. Use value converters only if you want enum type safety — not required for v1.

```csharp
// Data/AppDbContext.cs
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<BambuProduct> Products => Set<BambuProduct>();
    public DbSet<Spool> Spools => Set<Spool>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Owner>().HasData(
            new Owner { Id = 1, Name = "Me", IsMe = true }
        );
    }
}
```

Register:
```csharp
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=filament.db"));
```

### Migration Strategy: MigrateAsync on Startup

**Recommendation: call `MigrateAsync()` in `Program.cs` before `app.Run()`**

This is the correct approach for this scenario:
- Single instance (Windows service, one user, one machine) — no concurrent migration risk
- EF Core 9+ (which ships with .NET 9/10) automatically acquires a database-level lock before applying migrations, making this safe even if somehow two instances ran simultaneously
- The SQLite lock-table caveat (abandoned lock if process terminates mid-migration) is acceptable: the lock table is cleaned on next successful startup, and migrations run fast (milliseconds)
- Auto-migrate eliminates the need for a manual `dotnet ef database update` step after service install or update

**Do NOT use `EnsureCreatedAsync()`** — it bypasses migrations entirely and breaks future schema changes. This is an official Microsoft warning.

```csharp
// Program.cs — before app.Run()
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

**Migration files:** Generate code-first migrations with `dotnet ef migrations add <Name>`. Commit the `Migrations/` folder to source control. Never edit generated migration files manually.

**Seeding:** The `Owner { Id=1, Name="Me", IsMe=true }` seed is in `OnModelCreating` via `HasData`. EF handles idempotency automatically — it will not re-insert on repeated startups.

**Connection string:** For a Windows service, the SQLite path should be absolute or relative to the executable location. Use:
```csharp
var dbPath = Path.Combine(
    AppContext.BaseDirectory, "filament.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));
```
This works correctly whether the service is started by the SCM (which may have a different working directory than `dotnet run`) or run manually.

---

## Build Order

Build in this order — each phase has defined inputs from the previous.

**Phase 1 — Data layer foundation**
- Create entities (`Owner`, `BambuProduct`, `Spool`)
- Create `AppDbContext`, configure relationships and seed
- Add and apply initial EF migration
- Verify `filament.db` is created with correct schema on startup

**Phase 2 — Core CRUD API + minimal frontend**
- Implement `SpoolEndpoints`, `OwnerEndpoints`, `ProductEndpoints`
- Implement `SummaryEndpoints` and `BalanceEndpoints`
- Wire up static file serving (`UseStaticFiles`, `wwwroot/`)
- Build `index.html` with spool table, add/edit modal, owner management
- Verify full CRUD workflow works end-to-end

**Phase 3 — Background sync + scraper**
- Implement `SyncStateService` singleton
- Implement `BambuScraperService` (AngleSharp + ImageSharp)
- Implement `SyncWorker` (BackgroundService + Channel)
- Implement `SyncEndpoints` (POST trigger, GET status)
- Wire sync button + polling into frontend
- Verify scrape populates `BambuProduct` table and progress is visible

**Phase 4 — Windows service deployment**
- Add `UseWindowsService()` to `Program.cs`
- Verify absolute path for `filament.db`
- Install with `sc create` or `New-Service`
- Verify auto-start, migration on first run, browser access at `localhost:5000`

**Dependency notes:**
- Phase 2 frontend depends on Phase 1 API returning real data — mock data approach not needed since SQLite setup is trivial
- Phase 3 scraper depends on Phase 1 `BambuProduct` entity + Phase 2 context injection patterns being established
- Phase 4 has no code dependencies — it is a hosting configuration change only

---

## Sources

- [Background tasks with hosted services in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0) — HIGH confidence, official docs, updated 2025-08-28
- [Applying Migrations — EF Core Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) — HIGH confidence, official docs, updated 2026-04-16
- [Server-Sent Events in ASP.NET Core and .NET 10 — Milan Jovanović](https://www.milanjovanovic.tech/blog/server-sent-events-in-aspnetcore-and-dotnet-10) — MEDIUM confidence, community expert blog
- [Server-Sent Events in ASP.NET Core and .NET 10 — Khalid Abuhakmeh](https://khalidabuhakmeh.com/server-sent-events-in-aspnet-core-and-dotnet-10) — MEDIUM confidence, community expert blog
- [A Pragmatic Guide to Server-Sent Events (SSE) in ASP.NET Core — Roxeem](https://roxeem.com/2025/10/24/a-pragmatic-guide-to-server-sent-events-sse-in-asp-net-core/) — MEDIUM confidence, 2025 community article
- [How to Structure Minimal APIs — Milan Jovanović](https://www.milanjovanovic.tech/blog/how-to-structure-minimal-apis) — MEDIUM confidence, community expert blog
- [Real-Time Progress Updates for Long-Running API Tasks with SSE — Marius Schroeder](https://marius-schroeder.de/posts/real-time-progress-updates-for-long-running-api-tasks-with-server-sent-events-sse-in-asp-net-core/) — MEDIUM confidence, community blog
