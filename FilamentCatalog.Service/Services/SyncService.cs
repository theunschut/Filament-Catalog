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
        stateService.Start();

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
            stateService.Start(totalEstimate: entries.Length);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var colorName = entry.FilaColorName.TryGetValue("en", out var en) ? en : entry.FilaColorName.Values.FirstOrDefault() ?? "Unknown";
                var colorHex = StripAlpha(entry.FilaColor.FirstOrDefault() ?? "#888888");

                await UpsertAsync(
                    name: colorName,
                    material: entry.FilaType,
                    colorName: colorName,
                    colorHex: colorHex,
                    cancellationToken);

                stateService.IncrementProgress();
            }

            var syncTime = DateTime.UtcNow;
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

    private static string StripAlpha(string hex)
    {
        // "#FF6A13FF" (8-char) → "#FF6A13"; already 7-char values pass through unchanged
        return hex.Length == 9 ? hex[..7] : hex;
    }

    private async Task UpsertAsync(
        string name, string material, string colorName,
        string colorHex, CancellationToken cancellationToken)
    {
        var existing = await db.BambuProducts
            .FirstOrDefaultAsync(p => p.Name == name && p.Material == material, cancellationToken);

        if (existing != null)
        {
            existing.ColorName = colorName;
            existing.ColorHex = colorHex;
            existing.ColorSwatchUrl = null;
            existing.LastSyncedAt = DateTime.UtcNow;
        }
        else
        {
            db.BambuProducts.Add(new BambuProduct
            {
                Name = name,
                Material = material,
                ColorName = colorName,
                ColorHex = colorHex,
                ColorSwatchUrl = null,
                LastSyncedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
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
