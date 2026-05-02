using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public class SyncService(
    AppDbContext db,
    SyncStateService stateService,
    HttpClient httpClient,
    ILogger<SyncService> logger) : ISyncService
{
    // Shopify store URL — parameterized per RESEARCH.md anti-patterns
    private const string ShopifyBaseUrl = "https://bambulab.eu";

    public async Task SyncCatalogAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Catalog sync started");
        stateService.Start();

        try
        {
            var products = await FetchAllProductsAsync(cancellationToken);
            logger.LogInformation("Fetched {Count} Shopify products", products.Count);
            stateService.Start(totalEstimate: products.Sum(p => p.Variants.Length));

            foreach (var product in products)
            {
                foreach (var variant in product.Variants)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var material = variant.SelectedOptions
                        .FirstOrDefault(o => o.Name.Equals("Material", StringComparison.OrdinalIgnoreCase)
                                          || o.Name.Equals("Type", StringComparison.OrdinalIgnoreCase))
                        ?.Value ?? "Unknown";

                    var colorName = variant.SelectedOptions
                        .FirstOrDefault(o => o.Name.Equals("Color", StringComparison.OrdinalIgnoreCase)
                                          || o.Name.Equals("Colour", StringComparison.OrdinalIgnoreCase))
                        ?.Value ?? variant.Title;

                    // Find swatch image for this variant
                    var swatchUrl = (string?)null;
                    if (variant.ImageId.HasValue)
                    {
                        var img = product.Images.FirstOrDefault(i => i.Id == variant.ImageId.Value);
                        swatchUrl = img?.Src;
                    }
                    if (swatchUrl == null && product.Images.Length > 0)
                    {
                        swatchUrl = product.Images[0].Src;
                    }

                    var colorHex = "#888888";
                    if (!string.IsNullOrEmpty(swatchUrl))
                    {
                        try
                        {
                            colorHex = await ExtractDominantColorAsync(swatchUrl, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Color extraction failed for {Url}; using fallback", swatchUrl);
                        }
                    }

                    await UpsertProductAsync(
                        name: product.Title,
                        material: material,
                        colorName: colorName,
                        colorHex: colorHex,
                        colorSwatchUrl: swatchUrl,
                        cancellationToken: cancellationToken);

                    stateService.IncrementProgress();
                }
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

    private async Task<List<ShopifyProduct>> FetchAllProductsAsync(CancellationToken ct)
    {
        var allProducts = new List<ShopifyProduct>();
        var pageInfo = (string?)null;
        var pageCount = 0;
        const int limit = 250;
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        while (true)
        {
            pageCount++;
            var url = $"{ShopifyBaseUrl}/products.json?limit={limit}";
            if (!string.IsNullOrEmpty(pageInfo))
                url += $"&page_info={Uri.EscapeDataString(pageInfo)}";

            logger.LogInformation("Fetching Shopify products page {Page}", pageCount);
            var response = await httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Shopify API returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

            var content = await response.Content.ReadAsStringAsync(ct);
            var payload = JsonSerializer.Deserialize<ShopifyProductsResponse>(content, jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize Shopify products response");

            allProducts.AddRange(payload.Products);
            logger.LogInformation("Page {Page}: received {Count} products", pageCount, payload.Products.Length);

            // Cursor pagination via Link header
            if (!response.Headers.TryGetValues("Link", out var linkHeaders))
                break;

            var linkHeader = linkHeaders.FirstOrDefault() ?? "";
            if (!linkHeader.Contains("rel=\"next\""))
                break;

            pageInfo = ExtractPageInfo(linkHeader);
            if (string.IsNullOrEmpty(pageInfo))
                break;
        }

        return allProducts;
    }

    private static string? ExtractPageInfo(string linkHeader)
    {
        // Parse: <https://...?page_info=xxx>; rel="next"
        foreach (var part in linkHeader.Split(','))
        {
            if (!part.Contains("rel=\"next\""))
                continue;
            var match = Regex.Match(part, @"page_info=([^&>]+)");
            return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
        }
        return null;
    }

    private async Task<string> ExtractDominantColorAsync(string imageUrl, CancellationToken ct)
    {
        // Timeout for individual image downloads — don't let one slow image block the entire sync
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        using var imageStream = await httpClient.GetStreamAsync(imageUrl, cts.Token);
        using var image = Image.Load<Rgba32>(imageStream);

        // Center-crop to square per CLAUDE.md
        int size = Math.Min(image.Width, image.Height);
        int x = (image.Width - size) / 2;
        int y = (image.Height - size) / 2;
        image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, size, size)));

        // Calculate dominant color — filter alpha < 128 per CLAUDE.md
        long r = 0, g = 0, b = 0;
        int count = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (var pixel in row)
                {
                    if (pixel.A < 128) continue;
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    count++;
                }
            }
        });

        if (count == 0) return "#888888"; // Fully transparent — use fallback

        return $"#{(byte)(r / count):X2}{(byte)(g / count):X2}{(byte)(b / count):X2}";
    }

    private async Task UpsertProductAsync(
        string name, string material, string colorName,
        string colorHex, string? colorSwatchUrl, CancellationToken cancellationToken)
    {
        // Upsert: match on Name + Material (composite unique key per SYNC-04)
        var existing = await db.BambuProducts
            .FirstOrDefaultAsync(p => p.Name == name && p.Material == material, cancellationToken);

        if (existing != null)
        {
            existing.ColorName = colorName;
            existing.ColorHex = colorHex;
            existing.ColorSwatchUrl = colorSwatchUrl;
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
                ColorSwatchUrl = colorSwatchUrl,
                LastSyncedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

// Internal Shopify JSON DTOs — file-local, not exposed via API
internal class ShopifyProductsResponse
{
    public ShopifyProduct[] Products { get; set; } = [];
}

internal class ShopifyProduct
{
    public string Title { get; set; } = "";
    public ShopifyVariant[] Variants { get; set; } = [];
    public ShopifyImage[] Images { get; set; } = [];
}

internal class ShopifyVariant
{
    public string Title { get; set; } = "";
    [JsonPropertyName("options")]
    public ShopifyOption[] SelectedOptions { get; set; } = [];
    public long? ImageId { get; set; }
}

internal class ShopifyOption
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

internal class ShopifyImage
{
    public long Id { get; set; }
    public string Src { get; set; } = "";
}
