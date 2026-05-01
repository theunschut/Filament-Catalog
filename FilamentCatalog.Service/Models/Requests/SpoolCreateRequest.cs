public record SpoolCreateRequest(
    string Name, string Material, string? ColorHex, int OwnerId,
    int? WeightGrams, decimal? PricePaid,
    PaymentStatus PaymentStatus, SpoolStatus SpoolStatus, string? Notes);
