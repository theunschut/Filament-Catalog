public class SyncStatusDto
{
    public string Status { get; set; } = "idle"; // "idle" | "running" | "completed" | "error"
    public int ProcessedCount { get; set; }
    public int TotalEstimate { get; set; }
    public int? PercentComplete => TotalEstimate > 0
        ? (int)((ProcessedCount * 100.0) / TotalEstimate)
        : null;
    public string? ErrorMessage { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
