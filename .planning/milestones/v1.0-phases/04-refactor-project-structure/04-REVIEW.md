---
phase: 04-refactor-project-structure
reviewed: 2026-05-01T00:00:00Z
depth: standard
files_reviewed: 27
files_reviewed_list:
  - FilamentCatalog.EntityFramework/FilamentCatalog.EntityFramework.csproj
  - FilamentCatalog.EntityFramework/AppDbContext.cs
  - FilamentCatalog.EntityFramework/Models/Owner.cs
  - FilamentCatalog.EntityFramework/Models/Spool.cs
  - FilamentCatalog.EntityFramework/Models/PaymentStatus.cs
  - FilamentCatalog.EntityFramework/Models/SpoolStatus.cs
  - FilamentCatalog.Service/FilamentCatalog.Service.csproj
  - FilamentCatalog.slnx
  - FilamentCatalog.Service/Models/Exceptions/NotFoundException.cs
  - FilamentCatalog.Service/Models/Exceptions/DomainValidationException.cs
  - FilamentCatalog.Service/Models/Exceptions/ConflictException.cs
  - FilamentCatalog.Service/Models/Dtos/SummaryDto.cs
  - FilamentCatalog.Service/Models/Dtos/BalanceRowDto.cs
  - FilamentCatalog.Service/Models/Requests/OwnerCreateRequest.cs
  - FilamentCatalog.Service/Models/Requests/SpoolCreateRequest.cs
  - FilamentCatalog.Service/Models/Requests/SpoolUpdateRequest.cs
  - FilamentCatalog.Service/Services/IOwnerService.cs
  - FilamentCatalog.Service/Services/OwnerService.cs
  - FilamentCatalog.Service/Services/ISpoolService.cs
  - FilamentCatalog.Service/Services/SpoolService.cs
  - FilamentCatalog.Service/Services/ISummaryService.cs
  - FilamentCatalog.Service/Services/SummaryService.cs
  - FilamentCatalog.Service/Controllers/OwnersController.cs
  - FilamentCatalog.Service/Controllers/SpoolsController.cs
  - FilamentCatalog.Service/Controllers/SummaryController.cs
  - FilamentCatalog.Service/Controllers/BalanceController.cs
  - FilamentCatalog.Service/Program.cs
findings:
  critical: 2
  warning: 3
  info: 2
  total: 7
status: issues_found
---

# Phase 04: Code Review Report

**Reviewed:** 2026-05-01T00:00:00Z
**Depth:** standard
**Files Reviewed:** 27
**Status:** issues_found

## Summary

This phase extracts EF Core models and `AppDbContext` into a separate `FilamentCatalog.EntityFramework` project and introduces a service layer (`OwnerService`, `SpoolService`, `SummaryService`) plus `[ApiController]` MVC controllers in `FilamentCatalog.Service`. The structural refactor is sound, and conventions from CLAUDE.md (SQLite path, `UseDefaultFiles` order, `DateTime.UtcNow`, startup migration guard) are all respected.

Two blockers exist in the service layer: `SpoolService.CreateAsync` and `UpdateAsync` both return a `Spool` entity without its `Owner` navigation property loaded, producing an inconsistent (and partially null) API response. Three warnings cover a logic gap in the owed-balance calculation, missing enum range validation, and a stale bare-`catch` in the migrations-lock cleanup.

---

## Critical Issues

### CR-01: `SpoolService.CreateAsync` returns Spool with null Owner navigation property

**File:** `FilamentCatalog.Service/Services/SpoolService.cs:29-33`
**Issue:** After `db.SaveChangesAsync()`, the new `Spool` is returned directly to the controller, which serializes it with `Created(...)`. The `Owner` navigation property was never loaded — EF Core does not auto-populate it after an insert. The model declares `Owner` as `Owner Owner { get; set; } = null!;`, so the property is literally `null` at runtime. The serialized JSON response will contain `"owner": null`, which is inconsistent with `GET /api/spools` (which uses `.Include(s => s.Owner)` and returns the full owner object). Any frontend code that reads `spool.owner.name` on the `POST` response will throw a null-reference error.

**Fix:** After saving, re-load the spool with the Owner included, or explicitly load the navigation property:
```csharp
public async Task<Spool> CreateAsync(SpoolCreateRequest req)
{
    // ... validation ...
    db.Spools.Add(spool);
    await db.SaveChangesAsync();

    // Reload with Owner so the caller gets a complete entity
    await db.Entry(spool).Reference(s => s.Owner).LoadAsync();
    return spool;
}
```
Alternatively, query back by id: `return await db.Spools.Include(s => s.Owner).FirstAsync(s => s.Id == spool.Id);`

---

### CR-02: `SpoolService.UpdateAsync` returns Spool with null Owner navigation property

**File:** `FilamentCatalog.Service/Services/SpoolService.cs:37-56`
**Issue:** `FindAsync(id)` retrieves the spool by primary key without loading navigation properties. The updated spool is returned directly with `Ok(spool)`. The `Owner` navigation property is `null` at runtime (same `null!` suppressor as above). The `PUT /api/spools/{id}` response will serialize `"owner": null` while `GET /api/spools` returns fully-populated owner objects. This inconsistency can silently corrupt frontend state or cause null-dereferences in client code.

**Fix:** Load the Owner reference after saving (same approach as CR-01):
```csharp
await db.SaveChangesAsync();
await db.Entry(spool).Reference(s => s.Owner).LoadAsync();
return spool;
```

---

## Warnings

### WR-01: `SummaryService` counts full `PricePaid` as owed for `PaymentStatus.Partial`

**File:** `FilamentCatalog.Service/Services/SummaryService.cs:11-15` and `27-29`
**Issue:** The owed-amount calculation filters `s.PaymentStatus != PaymentStatus.Paid` — this includes `Partial` spools and sums the entire `PricePaid` as owed. A `Partial` payment means some amount has already been paid; adding the full price overstates the debt. The same logic appears in both `GetSummaryAsync` (for `totalOwed`) and `GetBalanceAsync` (for per-owner `owed`).

If `Partial` is intended to mean "the full price is still tracked but not yet fully received", this should be documented explicitly. If it means "partially paid", the model needs an `AmountPaid` field or the calculation needs to treat `Partial` differently (e.g., exclude it or flag it as approximate). As-is, the balance figures are numerically wrong for any spool with `PaymentStatus.Partial`.

**Fix (minimal — treat Partial the same as Paid for owed calculation, which at least avoids overstatement):**
```csharp
var totalOwed = spools
    .Where(s => meOwner is not null && s.OwnerId != meOwner.Id
                && s.PaymentStatus == PaymentStatus.Unpaid
                && s.PricePaid.HasValue)
    .Sum(s => s.PricePaid!.Value);
```
Or add an `AmountPaid` field to `Spool` to track partial amounts precisely.

---

### WR-02: Enum values from requests are not validated for range

**File:** `FilamentCatalog.Service/Services/SpoolService.cs:8-33` and `35-56`
**Issue:** `SpoolCreateRequest` and `SpoolUpdateRequest` carry `PaymentStatus` and `SpoolStatus` enum fields. With `JsonStringEnumConverter` registered, string inputs that don't match enum names cause a 400 at deserialization — that part is safe. However, if a client sends a raw integer (e.g., `"paymentStatus": 99`), the JSON deserializer will silently cast it to `(PaymentStatus)99`, which is an out-of-range enum value. EF Core will persist this value to the SQLite database as the integer `99`, and subsequent reads will produce an enum value that no code path handles. C# `switch` statements on the enum or `ToString()` calls will produce unexpected results.

**Fix:** Add enum-range guards in the service before the entity is constructed:
```csharp
if (!Enum.IsDefined(req.PaymentStatus))
    throw new DomainValidationException("Invalid PaymentStatus value.");
if (!Enum.IsDefined(req.SpoolStatus))
    throw new DomainValidationException("Invalid SpoolStatus value.");
```

---

### WR-03: Bare `catch` in `ClearStaleEfMigrationsLock` swallows all exceptions

**File:** `FilamentCatalog.Service/Program.cs:73-75`
**Issue:** The catch block that handles the "table does not exist on first run" case uses a bare `catch` with no exception type filter. This suppresses any error — including database connection failures, permission errors, or a corrupted database — that occurs during the DELETE statement. The comment says this is safe for the "table doesn't exist" scenario, but the over-broad catch hides real failures that would surface during `MigrateAsync()` a few lines later only as a confusing secondary error.

**Fix:** Catch only the expected SQLite exception for a missing table:
```csharp
catch (Microsoft.Data.Sqlite.SqliteException)
{
    // Table doesn't exist on first run — expected and safe to ignore
}
```
Or at minimum log the exception before swallowing it so diagnostics are preserved.

---

## Info

### IN-01: `SpoolCreateRequest` and `SpoolUpdateRequest` are identical records

**File:** `FilamentCatalog.Service/Models/Requests/SpoolCreateRequest.cs:1-4` and `SpoolUpdateRequest.cs:1-4`
**Issue:** Both records have identical fields with identical types. Keeping them separate is a valid design choice (create vs. update semantics can diverge), but if they remain identical they add maintenance overhead — a field addition must be made in two places. Consider documenting why they are separate, or consolidate with a type alias until they diverge.

**Fix:** Either add a comment explaining the intentional separation, or consolidate:
```csharp
// SpoolUpdateRequest is intentionally kept separate from SpoolCreateRequest
// to allow independent evolution of create vs. update field requirements.
```

---

### IN-02: All types declared in global namespace

**File:** `FilamentCatalog.EntityFramework/AppDbContext.cs`, `Models/Owner.cs`, `Models/Spool.cs`, `FilamentCatalog.Service/Services/OwnerService.cs`, etc.
**Issue:** None of the C# files declare a `namespace`. With `<ImplicitUsings>enable</ImplicitUsings>`, the code compiles, but all types land in the global namespace. This makes type discovery harder, prevents meaningful namespace-qualified references, and will cause ambiguity errors if any third-party package introduces a type with the same name (e.g., a future NuGet package that exports `Owner` or `Spool`). This affects every file in both projects.

**Fix:** Add file-scoped namespace declarations matching the project/folder structure:
```csharp
// FilamentCatalog.EntityFramework/Models/Owner.cs
namespace FilamentCatalog.EntityFramework.Models;

public class Owner { ... }
```

---

_Reviewed: 2026-05-01T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
