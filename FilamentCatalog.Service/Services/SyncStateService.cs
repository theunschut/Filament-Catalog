public class SyncStateService
{
    private readonly object _lock = new();
    private string _status = "idle";
    private int _processedCount = 0;
    private int _totalEstimate = 0;
    private string? _errorMessage = null;
    private DateTime? _lastSyncedAt = null;

    public SyncStatusDto GetStatus()
    {
        lock (_lock)
        {
            return new SyncStatusDto
            {
                Status = _status,
                ProcessedCount = _processedCount,
                TotalEstimate = _totalEstimate,
                ErrorMessage = _errorMessage,
                LastSyncedAt = _lastSyncedAt
            };
        }
    }

    public void Start(int totalEstimate = 0)
    {
        lock (_lock)
        {
            _status = "running";
            _processedCount = 0;
            _totalEstimate = totalEstimate;
            _errorMessage = null;
            // Do NOT reset _lastSyncedAt — preserve last successful sync time during new run
        }
    }

    public void IncrementProgress()
    {
        lock (_lock) { _processedCount++; }
    }

    public void Complete(DateTime syncTime)
    {
        lock (_lock)
        {
            _status = "completed";
            _lastSyncedAt = syncTime;
        }
    }

    public void Error(string message)
    {
        lock (_lock)
        {
            _status = "error";
            _errorMessage = message;
        }
    }
}
