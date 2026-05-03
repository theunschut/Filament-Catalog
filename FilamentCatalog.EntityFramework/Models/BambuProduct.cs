public class BambuProduct
{
    public int Id { get; set; }
    public required string Name { get; set; }          // Color name (unique per material)
    public required string Material { get; set; }      // Variant option value (e.g. "PLA")
    public required string ColorName { get; set; }     // Variant color name
    public required string ColorHex { get; set; }      // Extracted dominant color (#RRGGBB)
    public string? ColorSwatchUrl { get; set; }        // Shopify swatch image URL
    public DateTime LastSyncedAt { get; set; }         // UTC timestamp — always DateTime.UtcNow
}
