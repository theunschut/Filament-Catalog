public class Spool
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Material { get; set; }
    public required string ColorHex { get; set; }
    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;
    public int? WeightGrams { get; set; }
    public decimal? PricePaid { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public SpoolStatus SpoolStatus { get; set; } = SpoolStatus.Sealed;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
