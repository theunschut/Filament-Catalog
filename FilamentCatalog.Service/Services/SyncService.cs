using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

public class SyncService(
    AppDbContext db,
    SyncStateService stateService,
    ILogger<SyncService> logger) : ISyncService
{
    private static readonly string[] CandidatePaths =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BambuStudio", "system", "BBL", "filament", "filaments_color_codes.json"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Bambu Studio", "resources", "profiles", "BBL", "filament", "filaments_color_codes.json")
    ];

    public async Task SyncCatalogAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Catalog sync started");

        try
        {
            var filePath = CandidatePaths.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException(
                    "filaments_color_codes.json not found. Is Bambu Studio installed?",
                    CandidatePaths[0]);

            logger.LogInformation("Reading filament catalog from {Path}", filePath);

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var payload = JsonSerializer.Deserialize<FilamentColorFile>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to deserialize filaments_color_codes.json");

            var entries = payload.Data;
            logger.LogInformation("Loaded {Count} filament color entries", entries.Length);

            // CR-01 fix: call Start once, with totalEstimate known upfront
            stateService.Start(totalEstimate: entries.Length);

            var syncTime = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var colorName = entry.FilaColorName.TryGetValue("en", out var en)
                    ? en
                    : entry.FilaColorName.Values.FirstOrDefault() ?? "Unknown";

                // CR-04 fix: validate hex before storing — reject malformed values
                var rawHex = entry.FilaColor.FirstOrDefault() ?? "";
                var colorHex = NormalizeHex(rawHex);

                // CR-03 fix: Name stores the color name (unique key component with Material);
                // product line is derived at query time via Material field.
                UpsertTracked(
                    colorName: colorName,
                    material: entry.FilaType,
                    colorHex: colorHex,
                    syncTime: syncTime);

                stateService.IncrementProgress();
            }

            // CR-02 fix: single SaveChangesAsync after the loop — not per-row
            await db.SaveChangesAsync(cancellationToken);

            stateService.Complete(syncTime);
            logger.LogInformation("Catalog sync completed at {SyncTime}", syncTime);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Catalog sync cancelled");
            stateService.Error("Sync was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalog sync failed");
            stateService.Error(ex.Message);
        }
    }

    /// <summary>
    /// Accepts "#RRGGBBAA" (strips alpha) or "#RRGGBB". Rejects anything else and returns fallback.
    /// </summary>
    private static string NormalizeHex(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "#888888";

        // Strip alpha from 9-char "#RRGGBBAA" → "#RRGGBB"
        var hex = raw.Length == 9 && raw[0] == '#' ? raw[..7] : raw;

        // Validate: must be "#RRGGBB"
        if (hex.Length == 7 && hex[0] == '#' &&
            hex[1..].All(c => c is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f')))
        {
            return hex.ToUpperInvariant();
        }

        return "#888888"; // fallback for unexpected formats
    }

    /// <summary>
    /// Upsert via EF change tracking — no per-row SaveChanges.
    /// Matches on (ColorName, Material) which is the natural unique key from the source data.
    /// Name is set to ColorName (serves as the DB unique constraint column alongside Material).
    /// </summary>
    private void UpsertTracked(string colorName, string material, string colorHex, DateTime syncTime)
    {
        var existing = db.BambuProducts.Local
            .FirstOrDefault(p => p.Name == colorName && p.Material == material)
            ?? db.BambuProducts
                .AsNoTracking()
                .FirstOrDefault(p => p.Name == colorName && p.Material == material);

        if (existing != null)
        {
            db.BambuProducts.Attach(existing);
            existing.ColorHex = colorHex;
            existing.LastSyncedAt = syncTime;
        }
        else
        {
            db.BambuProducts.Add(new BambuProduct
            {
                Name = colorName,
                Material = material,
                ColorName = colorName,
                ColorHex = colorHex,
                ColorSwatchUrl = null,
                LastSyncedAt = syncTime
            });
        }
    }
}

// DTOs for filaments_color_codes.json
internal class FilamentColorFile
{
    public FilamentColorEntry[] Data { get; set; } = [];
}

internal class FilamentColorEntry
{
    [JsonPropertyName("fila_type")]
    public string FilaType { get; set; } = "";

    [JsonPropertyName("fila_color_name")]
    public Dictionary<string, string> FilaColorName { get; set; } = [];

    [JsonPropertyName("fila_color")]
    public string[] FilaColor { get; set; } = [];
}
