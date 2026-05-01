public record BalanceRowDto(
    int OwnerId, string OwnerName, int SpoolCount,
    decimal Value, decimal Owed, bool HasUnpriced);
