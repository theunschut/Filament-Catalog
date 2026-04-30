# Pitfalls Research

**Project:** Filament Catalog
**Domain:** Local Windows service — web scraping + image processing + SQLite
**Researched:** 2026-04-30
**Overall confidence:** HIGH (verified against official docs and confirmed sources)

---

## Scraping Pitfalls (Bambu Lab store)

### CRITICAL: The store URL redirects and returns 402 without browser context

**What goes wrong:** `https://store.bambulab.com/en-eu/collections/filament` immediately 302-redirects to `https://eu.store.bambulab.com/en-eu/collections/filament`. A plain `HttpClient` with no headers receives a 402 (Payment Required / geo-block / bot wall) response. The page is never served to the scraper.

**Why it happens:** The EU Bambu Lab store runs on Shopify and is protected at the CDN/infrastructure level. Without a realistic browser `User-Agent`, `Accept`, `Accept-Language`, and `Accept-Encoding` header set, requests are flagged as automated immediately.

**How to detect early:** Add a smoke test in phase 1: make the request and assert the status code is 200. A 402/403/503 response is a clear early signal.

**Prevention:**
- Set a full realistic header block on `HttpClient.DefaultRequestHeaders`:
  ```csharp
  client.DefaultRequestHeaders.Add("User-Agent",
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
  client.DefaultRequestHeaders.Add("Accept",
      "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
  client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
  ```
- Follow redirects automatically (`AllowAutoRedirect = true`, which is the default).
- Build in a fallback: if the HTML endpoint fails, try the Shopify JSON API (see below).

**Phase:** Address in Phase 1 (scraper foundation). Do not assume the plain GET works — verify on the first day.

---

### CRITICAL: The page may be JS-rendered — AngleSharp cannot execute JavaScript

**What goes wrong:** AngleSharp is a static HTML parser. It downloads raw HTML and parses the DOM — it does not execute JavaScript. If Bambu Lab's Shopify collection page renders its product listing client-side (loading products via XHR/fetch into a React/Liquid shell), AngleSharp will parse an empty product grid with no filament entries.

**Why it happens:** Many modern Shopify storefronts lazy-load collection products via JavaScript after initial page load. The initial HTML response contains only the shell.

**How to detect early:** After fetching the page with AngleSharp, query for product card elements (e.g., `.product-card`, `[data-product-id]`, `article`, etc.). If zero elements match, the page is JS-rendered.

**Prevention — Use the Shopify JSON API instead of HTML parsing:**

Shopify publicly exposes a machine-readable endpoint on every store:
```
https://eu.store.bambulab.com/en-eu/collections/filament/products.json?limit=250
```
This returns structured JSON (product title, variants, images, prices) without requiring JavaScript execution, without a browser UA, and without any HTML parsing. This is a documented, intentional Shopify feature.

- Pagination: add `?page=2`, `?page=3`, etc., until an empty `products` array is returned.
- No AngleSharp is needed for the product listing itself — only potentially for individual product detail pages if additional data is needed.
- This approach also makes the scraper far more robust to future HTML template changes.

**Fallback chain:**
1. Try `products.json` — preferred, no JS needed.
2. If 402/403, try the HTML page with browser headers and parse with AngleSharp.
3. If HTML is still empty of products, log an error and surface it to the user ("Sync failed — store layout changed").

**Phase:** Address in Phase 1. The JSON API should be the primary strategy, not the fallback.

---

### MODERATE: Page structure changes break HTML selectors silently

**What goes wrong:** Shopify store owners (including Bambu Lab) can update their theme at any time. CSS classes, HTML structure, and data attributes change. An AngleSharp scraper that relies on `.product-card h2 a` will return zero results after a theme update, with no exception thrown — just an empty sync.

**Why it happens:** Web scraping against HTML structure is inherently fragile. The scraper has no contract with the site.

**How to detect early:** After a sync, assert that the result count is non-zero. Log and alert if sync returns fewer products than the previous run by more than 50%.

**Prevention:**
- Prefer the Shopify JSON API (eliminates this pitfall entirely for the listing).
- If HTML scraping is needed for any field, use multiple selector fallbacks.
- Store the last-known product count and warn the user if a sync returns significantly fewer items.

**Phase:** Phase 1 for detection logic; ongoing operational concern.

---

### MODERATE: Rate limiting and CDN throttling on repeated syncs

**What goes wrong:** Fetching 100+ swatch images in rapid succession from Shopify's CDN (`cdn.shopify.com`) triggers rate limiting. Responses become 429 or silent connection resets. This is separate from scraping the collection page.

**Why it happens:** CDNs implement per-IP rate limiting on asset downloads. The default `HttpClient` with no delay will hammer the CDN.

**How to detect early:** Check for 429 responses or `HttpRequestException` during image batch downloads.

**Prevention:**
- Throttle image downloads: use `SemaphoreSlim` to limit concurrency to 3–5 simultaneous downloads.
- Add a small delay between requests (100–300ms).
- Implement a Polly retry policy with exponential backoff that respects `Retry-After` headers on 429.
- Cache downloaded images locally (by URL hash) so re-syncs don't re-download unchanged images.

**Phase:** Phase 1 (scraper) and Phase 2 (image processing). Image caching is important for developer iteration speed too.

---

### LOW: Bambu Lab has had Cloudflare verification issues in their own apps

**What goes wrong:** Community reports (GitHub issue #6809 in BambuStudio) show Cloudflare verification loops on Bambu Lab properties. If Cloudflare is active on the store, it may issue JS challenges that neither AngleSharp nor plain `HttpClient` can solve.

**Why it happens:** Cloudflare's bot detection uses TLS fingerprinting, JS execution, behavioral analysis, and IP reputation. A .NET `HttpClient` fails multiple of these checks.

**How to detect early:** A response containing `cf-ray` header, a body matching "Checking your browser" or "Just a moment", or a 503 from Cloudflare indicates this is active.

**Prevention:**
- The Shopify JSON API is less aggressively protected than HTML pages and may bypass this.
- If the JSON API is also blocked, consider: (a) user-initiated sync where the user navigates to the page in a browser first and the app reads an exported file, or (b) manual catalog entry as a fallback.
- Do NOT embed a headless browser (Playwright/Puppeteer) unless all simpler approaches fail — it adds significant complexity for a local tool.

**Phase:** Phase 1. Detect early; have a degraded-mode fallback designed.

---

## Image Processing Pitfalls (ImageSharp color extraction)

### CRITICAL: ImageSharp objects must be disposed — memory leaks in batch processing

**What goes wrong:** `Image<Rgba32>` allocates unmanaged memory from a large pool (~4MB chunks). If `Image.Load(...)` is called in a loop without `using` or explicit `.Dispose()`, the GC finalizer eventually reclaims the memory, but in a batch of 100+ images the GC cannot keep pace. `OutOfMemoryException` follows.

**Why it happens:** ImageSharp 2.0+ moved to pooled unmanaged memory for performance. This means finalizer-based cleanup works but is too slow for bulk processing. The `TotalUndisposedAllocationCount` diagnostic can grow by 20–100 per image in loops without disposal.

**How to detect early:** Enable ImageSharp diagnostics:
```csharp
SixLabors.ImageSharp.Diagnostics.MemoryDiagnostics.UndisposedAllocationCount
```
Assert it is 0 after each sync in tests.

**Prevention:**
- Always wrap `Image.Load(...)` in a `using` statement.
- Process images one at a time in a `foreach` loop, not via parallel LINQ that holds multiple images in memory simultaneously.
- If parallel processing is needed, limit concurrency with `SemaphoreSlim(3)`.

```csharp
// Correct pattern
using var image = await Image.LoadAsync<Rgba32>(stream);
var dominant = ExtractDominantColor(image);
// image disposed here automatically
```

**Phase:** Phase 2 (image processing). Enforce via code review and diagnostic assertion in tests.

---

### CRITICAL: Transparent pixels in swatch PNGs skew dominant color toward black or white

**What goes wrong:** Filament color swatches are typically PNG images with transparency (alpha channel). A naive dominant color algorithm that samples all pixels will include fully transparent pixels (`Rgba32(0,0,0,0)`) in the color count. This pushes the result toward black or muddies the calculation. ImageSharp also has a known issue where applying some processors near transparent edges produces color bleed to black.

**Why it happens:** Swatch images use a circular or shaped crop with transparent surrounds. Transparent pixels are valid `Rgba32` values but carry no visual color information.

**Prevention:**
- Filter out pixels with low alpha before color counting:
  ```csharp
  if (pixel.A < 128) continue; // skip transparent/semi-transparent
  ```
- For dominant color via median cut or k-means, only feed opaque pixels (A >= 128) into the algorithm.
- Flatten to a white background before processing if alpha handling is complex: `image.Mutate(x => x.BackgroundColor(Color.White));`

**How to detect early:** Test with a known circular swatch PNG. Assert the returned color is not black (R=0,G=0,B=0) or white (R=255,G=255,B=255) when the swatch is a saturated color.

**Phase:** Phase 2. The alpha filter is a one-liner but must not be forgotten.

---

### MODERATE: HTTP download errors during image batch are not retried by default

**What goes wrong:** During a sync of 100+ swatch images, transient network errors (connection reset, timeout, 503) on individual image downloads cause unhandled `HttpRequestException`, which stops the entire sync.

**Prevention:**
- Wrap each image download in a try-catch; log failures and store a null/default color rather than aborting the whole batch.
- Add a Polly retry with 3 attempts and exponential backoff for image downloads.
- On sync completion, report "N images failed to download" to the user rather than silently skipping.

**Phase:** Phase 2. Handle gracefully — a missing color swatch is not worth aborting a 100-product sync.

---

### LOW: Large swatch images waste memory — resize before color extraction

**What goes wrong:** If a swatch image is unexpectedly large (e.g., 1200x1200px), loading the full image just to extract dominant color loads ~6MB of pixel data when a 50x50 thumbnail would produce the same result.

**Prevention:**
- Resize to a small thumbnail (64x64) before color extraction:
  ```csharp
  image.Mutate(x => x.Resize(64, 64));
  ```
  This reduces pixel iteration time and peak memory by 99%.

**Phase:** Phase 2. Simple one-liner; add it as a standard step in the color extraction pipeline.

---

## Windows Service Pitfalls

### CRITICAL: `Directory.GetCurrentDirectory()` returns `C:\Windows\System32` in a service

**What goes wrong:** A Windows service's working directory is `C:\Windows\System32`, not the application folder. Any code that uses a relative path for the SQLite database file (e.g., `"Data Source=catalog.db"`) will try to open or create `C:\Windows\System32\catalog.db`. The service account typically lacks write permission there, causing the app to fail on startup with "unable to open database file."

**Why it happens:** This is documented behavior: `UseWindowsService()` sets `ContentRootPath` to `AppContext.BaseDirectory`, but does NOT change `Directory.GetCurrentDirectory()`.

**How to detect early:** Run the published service binary once as a service (not via `dotnet run`). If SQLite cannot open the database, check the actual path being constructed.

**Prevention:**
- Build all file paths from `AppContext.BaseDirectory` or `IHostEnvironment.ContentRootPath`:
  ```csharp
  var dbPath = Path.Combine(AppContext.BaseDirectory, "catalog.db");
  // or for user data:
  var dbPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
      "FilamentCatalog", "catalog.db");
  ```
- Prefer `CommonApplicationData` (`C:\ProgramData\FilamentCatalog\`) for the database file; this is the conventional location for service data and is readable/writable by all service accounts.
- Ensure the directory exists before EF Core tries to open/create the file:
  ```csharp
  Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
  ```

**Phase:** Phase 1 (service scaffolding). Get this right from day one; it will not surface during `dotnet run` development, only in deployed service mode.

---

### CRITICAL: Port 5000 conflict on service restart or second instance

**What goes wrong:** If the service crashes and is restarted by the SCM before the previous process fully releases port 5000, the new process fails to bind and the service enters a crash-restart loop. Also, other local development instances of ASP.NET Core apps default to port 5000.

**Prevention:**
- Pin the port explicitly in `appsettings.json` rather than relying on the default:
  ```json
  "Urls": "http://localhost:5000"
  ```
- Consider using a less-common port (e.g., 5100) to reduce conflicts with other development servers.
- Add a meaningful startup failure message: catch `IOException` on `app.Run()` and log "Port 5000 is already in use."

**Phase:** Phase 1. Set the port explicitly from the first commit.

---

### MODERATE: Service account lacks network access for outbound HTTP (scraping)

**What goes wrong:** If the service is installed under a restricted account (e.g., `NT AUTHORITY\LocalService`), outbound HTTP requests to `eu.store.bambulab.com` may be blocked by Windows Firewall or the account may lack network access rights.

**Why it happens:** `LocalService` has minimal network privileges. `LocalSystem` has full network access but is over-privileged. `NetworkService` is the appropriate built-in account for services that need outbound internet access.

**Prevention:**
- Install the service under `NT AUTHORITY\NetworkService` or a dedicated local user account.
- Document the required service account in the install script.
- Test network access from the service account by temporarily running `sc create ... obj= "NT AUTHORITY\NetworkService"`.

**Phase:** Phase 1. Document in the deployment script; test with the actual service account before calling it done.

---

### MODERATE: `appsettings.json` not found when running as a service

**What goes wrong:** When deployed, if `appsettings.json` is not in `AppContext.BaseDirectory` (e.g., it was excluded from the publish profile), the app starts with no configuration. This manifests as a missing connection string, causing EF Core to throw on startup.

**Why it happens:** `UseWindowsService()` correctly sets `ContentRootPath` to `AppContext.BaseDirectory`, and `CreateDefaultBuilder` loads `appsettings.json` from there. But if the publish step excludes config files or they are placed in the wrong directory, they are not found.

**Prevention:**
- Include `appsettings.json` in the publish output (`<CopyToPublishDirectory>Always</CopyToPublishDirectory>`).
- Validate the connection string is present on startup; fail fast with a clear error if it is missing.

**Phase:** Phase 1. Validate the publish profile early.

---

### LOW: Event log source creation requires admin rights — startup logs silently fail

**What goes wrong:** `AddWindowsService()` enables Windows Event Log logging using the application name as the event source. Creating a new event source requires administrator privileges. If the service account is not an admin, the event source cannot be registered and event log entries are silently dropped. A warning is logged to the Application source, but this may go unnoticed.

**Prevention:**
- Pre-register the event source during service installation (requires admin, which the install step has):
  ```powershell
  New-EventLog -LogName Application -Source "FilamentCatalog"
  ```
- Alternatively, configure structured file logging (Serilog rolling file, or `Microsoft.Extensions.Logging` file provider) as the primary log output rather than relying on Event Log.

**Phase:** Phase 1. Decide on logging strategy upfront; a rolling file log is more useful for a local app than Event Log.

---

## SQLite / EF Core Pitfalls

### CRITICAL: Abandoned `__EFMigrationsLock` prevents all future migrations

**What goes wrong:** EF Core 9+ (including EF10) uses a `__EFMigrationsLock` table in SQLite to prevent concurrent migration runs. If the service process is killed (crash, forced stop, power loss) while a migration is running, the lock row is never removed. All subsequent service startups that call `MigrateAsync()` will block indefinitely waiting for the lock, effectively bricking the database.

**Why it happens:** SQLite does not support session-level application locks that auto-release on disconnect. EF Core emulates this with a table row. If the row is never deleted (crash scenario), it stays forever.

**How to detect early:** If the service starts and hangs at the migration step with no timeout, this is the cause. Check for a row in `__EFMigrationsLock`.

**Prevention:**
- Add a startup check that clears stale locks:
  ```csharp
  // On startup, before MigrateAsync:
  await db.Database.ExecuteSqlRawAsync(
      "DELETE FROM __EFMigrationsLock WHERE Id = 1"); // safe no-op if table/row absent
  ```
  Note: wrap in try-catch since the table may not exist yet on first run.
- Alternatively, use a startup timeout: if `MigrateAsync` does not complete within 10 seconds, log an error and either clear the lock or fail fast.
- Document the manual recovery step: `DELETE FROM __EFMigrationsLock;`

**Phase:** Phase 1 (service scaffold + database setup). Address before any production use.

---

### CRITICAL: SQLite WAL mode — stale `.db-wal` and `.db-shm` files after crash

**What goes wrong:** EF Core for SQLite enables WAL (Write-Ahead Logging) mode by default. WAL creates two sidecar files: `catalog.db-wal` and `catalog.db-shm`. If the service crashes mid-write, these files are left on disk. On next startup, SQLite automatically recovers them, but if the files are partially corrupted, the database may fail to open.

**Why it happens:** WAL files are not cleaned up by the OS; they require a clean SQLite checkpoint. After an unclean shutdown, auto-recovery normally works, but interrupted writes can leave the WAL in an inconsistent state.

**How to detect early:** If the database fails to open on service restart with a "database disk image is malformed" error, WAL corruption is the likely cause.

**Prevention:**
- WAL mode is correct for this app and should not be disabled (it allows concurrent reads during writes).
- Do NOT delete `*.db-wal` and `*.db-shm` manually — this will corrupt the database. Let SQLite handle recovery.
- Back up by copying only when WAL is checkpointed (i.e., after a clean shutdown). Alternatively, use `VACUUM INTO` for backup.
- If corruption occurs: restore from backup, or use the SQLite `.recover` command.

**Phase:** Operational concern. Document recovery procedure in the deployment README.

---

### HIGH: EF Core 10 + Microsoft.Data.Sqlite 10 — DateTime timezone behavior changed

**What goes wrong:** `Microsoft.Data.Sqlite` 10.0 introduced three **high-impact breaking changes** in `DateTime`/`DateTimeOffset` handling:

1. `GetDateTimeOffset` on a timestamp without an offset now assumes **UTC** (previously assumed local time zone).
2. Writing a `DateTimeOffset` to a REAL column now converts to UTC first.
3. `GetDateTime` on a timestamp with an offset now returns UTC with `DateTimeKind.Utc`.

If EF Core entities use `DateTime` or `DateTimeOffset` fields and the database was populated with a previous version, values may be read back with incorrect times after upgrade.

**Impact for this project:** Sync timestamps (`LastSyncedAt`) and audit dates (`CreatedAt`, `UpdatedAt`) are affected. If stored without UTC and later read as UTC, timestamps will be off by the machine's timezone offset.

**Prevention:**
- **Always store `DateTime` values as UTC** from the start: use `DateTime.UtcNow` not `DateTime.Now`. This aligns with the new behavior and makes the breaking change a no-op.
- Do not use `DateTimeOffset` in EF Core entities mapped to SQLite — use `DateTime` with UTC convention.
- Add a convention in `OnModelConfiguring` or a value converter to enforce UTC storage.

**Phase:** Phase 1. Establish the UTC-only convention before any migrations are created. It is far harder to fix after data is written.

---

### MODERATE: SQLite does not support all EF Core migration operations — rebuilds can fail

**What goes wrong:** SQLite does not support `ALTER TABLE DROP COLUMN` (well, it does in newer SQLite versions, but some operations like `AddForeignKey` and `AlterColumn` require a full table rebuild). EF Core handles this by creating a new table, copying data, dropping the old, and renaming. If a migration rebuild fails mid-operation (crash, or a column/constraint not in the EF model), the table is left in an inconsistent state.

**Prevention:**
- Keep the schema simple — avoid operations that trigger rebuilds where possible.
- Test all migrations on a real database file before shipping.
- For any migration that involves a rebuild, take a database backup first (copy the `.db` file).
- Do not apply migrations to a production database without testing on a copy.

**Phase:** Relevant for every schema migration. Note in the development workflow.

---

### MODERATE: EF Core connection pooling and SQLite — "database is locked" under concurrent requests

**What goes wrong:** Multiple concurrent HTTP requests (e.g., a user clicks "sync" while the UI is polling for status) create multiple `DbContext` instances. In SQLite, only one writer can hold the database lock at a time. Without WAL mode, concurrent writes fail immediately with "database is locked." With WAL mode, concurrent reads work but simultaneous writes still contend.

**Why it happens:** ASP.NET Core creates a new `DbContext` per request (scoped lifetime). If two write operations overlap, the second gets `SqliteException: database is locked`.

**Prevention:**
- Enable WAL mode (EF Core SQLite does this by default — confirm it is active).
- Add `connection string: "Data Source=catalog.db;Cache=Shared"` only if sharing connections between contexts — but for this app, per-request contexts should be fine.
- For the sync operation (bulk write), run it as a single sequential background task; do not allow concurrent syncs. Use a flag/semaphore: if a sync is in progress, reject new sync requests.
- Configure a busy timeout so SQLite waits instead of failing immediately:
  ```csharp
  optionsBuilder.UseSqlite("Data Source=catalog.db", o =>
      o.CommandTimeout(10)); // wait up to 10 seconds
  ```
  Or set directly: `PRAGMA busy_timeout = 5000;` on connection open.

**Phase:** Phase 2 (sync background task). Design the sync to be non-concurrent from the start.

---

### LOW: EF Core 10 tools require `--framework` for multi-targeted projects

**What goes wrong:** If the `.csproj` uses `<TargetFrameworks>` (plural) instead of `<TargetFramework>` (singular), all `dotnet ef` commands (`migrations add`, `database update`) fail with: "The project targets multiple frameworks. Use the --framework option."

**Prevention:**
- Use `<TargetFramework>net10.0</TargetFramework>` (singular) — this project has no reason to multi-target.

**Phase:** Phase 1. Non-issue if the project is set up correctly from the start.

---

## General .NET Local App Pitfalls

### MODERATE: `HttpClient` instantiated per-request causes socket exhaustion

**What goes wrong:** Creating `new HttpClient()` for each scrape/image download request exhausts the socket pool. Even after `HttpClient.Dispose()`, the underlying `HttpClientHandler` holds TCP sockets in TIME_WAIT for ~4 minutes. Under high concurrency (batch image downloads), this manifests as "Only one usage of each socket address is normally permitted."

**Prevention:**
- Use `IHttpClientFactory` (registered as `builder.Services.AddHttpClient()`), which manages `HttpClientHandler` lifetimes correctly.
- Or use a single static/singleton `HttpClient` instance for the app (acceptable for a single-user local app).
- Never `new HttpClient()` in a method body.

**Phase:** Phase 1 (service scaffold). Set up `IHttpClientFactory` from the first commit.

---

### MODERATE: Sync runs as a long operation on the request thread — UI freezes / timeout

**What goes wrong:** If the sync endpoint (`POST /api/sync`) runs the entire scrape + image download + database write synchronously on the request handler, the HTTP request will not return until sync completes (possibly 30–120 seconds). The browser may show a spinner with no feedback. If any proxy or browser timeout fires, the response is dropped but sync continues in the background — the UI has no way to know.

**Prevention:**
- Run sync as a `BackgroundService` task or `Task.Run` with a completion flag.
- The sync endpoint returns 202 Accepted immediately, with a `/api/sync/status` polling endpoint.
- Surface progress to the UI via SSE or periodic polling (plain polling is simpler for this app).

**Phase:** Phase 2 (sync feature). Design the async pattern before implementing sync.

---

### LOW: `appsettings.Development.json` leaks into the published service

**What goes wrong:** If `appsettings.Development.json` is present in the publish output and `ASPNETCORE_ENVIRONMENT` is not set for the service, the service runs with `Development` environment settings (e.g., detailed error pages, development database paths).

**Prevention:**
- Set the environment via the service registration or an environment variable:
  ```powershell
  New-Service ... -BinaryPathName "myapp.exe" -Environment "Production"
  ```
  Or via the registry key `HKLM\SYSTEM\CurrentControlSet\Services\{ServiceName}\Environment` → `ASPNETCORE_ENVIRONMENT=Production`.
- Exclude `appsettings.Development.json` from the publish profile.

**Phase:** Phase 1. Establish environment separation in the first deployment test.

---

### LOW: Static file serving from wrong root when running as a service

**What goes wrong:** `app.UseStaticFiles()` serves from `ContentRootPath/wwwroot`. Since `UseWindowsService()` sets `ContentRootPath` to `AppContext.BaseDirectory`, the `wwwroot` folder must be in the publish output directory. If it is not (e.g., omitted from publish profile), all CSS/JS/HTML returns 404.

**Prevention:**
- Ensure `wwwroot` is included in the publish output. Verify with a post-publish file listing check.
- In the publish profile, set `<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>` for all static assets.

**Phase:** Phase 1. Verify during the first `dotnet publish` + service install cycle.

---

## Phase-Specific Warnings Summary

| Phase | Topic | Likely Pitfall | Mitigation |
|-------|-------|----------------|------------|
| 1 | Service scaffold | Working directory / SQLite path | Use `AppContext.BaseDirectory` or `ProgramData` |
| 1 | Service scaffold | Port 5000 conflict | Explicit URL binding in config |
| 1 | Service scaffold | Event log source creation | Pre-register source or use file logging |
| 1 | Database setup | DateTime UTC convention | Use `DateTime.UtcNow` only, never `DateTime.Now` |
| 1 | Database setup | `__EFMigrationsLock` on crash | Add startup lock-clear with try-catch |
| 1 | HTTP clients | Socket exhaustion | Use `IHttpClientFactory` from the start |
| 1 | Scraping | Bambu store returns 402 | Set browser-like headers; try Shopify JSON API |
| 2 | Scraping | JS-rendered page | Use `products.json` Shopify endpoint |
| 2 | Image processing | ImageSharp memory leak | `using` on every `Image.Load(...)` call |
| 2 | Image processing | Transparent PNG skews color | Filter pixels with `A < 128` |
| 2 | Sync design | Long-running request blocks UI | 202 + polling pattern from the start |
| 2 | Sync design | Concurrent sync + DB lock | Single-instance sync semaphore |
| All | Schema changes | SQLite migration rebuild failures | Test migrations on a copy before applying |

---

## Sources

- Microsoft Docs — Host ASP.NET Core in a Windows Service: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0
- Microsoft Docs — EF Core 10 Breaking Changes: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes
- Microsoft Docs — SQLite Provider Limitations: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- SixLabors ImageSharp Memory Management: https://docs.sixlabors.com/articles/imagesharp/memorymanagement.html
- ImageSharp alpha channel color bleed issue: https://github.com/SixLabors/ImageSharp/issues/428
- ImageSharp dominant color extraction gist: https://gist.github.com/JimBobSquarePants/12e0ef5d904d03110febea196cf1d6ee
- Shopify products.json scraping guide: https://dev.to/dentedlogic/the-shopify-productsjson-trick-scrape-any-store-25x-faster-with-python-4p95
- Bambu Studio Cloudflare loop issue: https://github.com/bambulab/BambuStudio/issues/6809
- EF Core SQLite `__EFMigrationsLock` (EF9+ docs, applies to EF10): https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- Microsoft Docs — HTTP client rate limiting with Polly: https://learn.microsoft.com/en-us/dotnet/core/extensions/http-ratelimiter
- SQLite WAL mode: https://sqlite.org/wal.html
- EF Core WAL issue tracking: https://github.com/dotnet/efcore/issues/14059
