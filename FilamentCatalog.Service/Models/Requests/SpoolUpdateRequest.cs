public record SpoolUpdateRequest(
    string Name, string Material, string? ColorHex, int OwnerId,
    int? WeightGrams, decimal? PricePaid,
    PaymentStatus PaymentStatus, SpoolStatus SpoolStatus, string? Notes);
