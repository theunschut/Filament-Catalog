using System.Threading.Channels;

public record SyncJob(int Id = 0);

public class SyncBackgroundService : BackgroundService
{
    private readonly Channel<SyncJob> _channel;
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(
        Channel<SyncJob> channel,
        ISyncService syncService,
        ILogger<SyncBackgroundService> logger)
    {
        _channel = channel;
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync background service started");

        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Processing sync job {JobId}", job.Id);
            try
            {
                await _syncService.SyncCatalogAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Sync job {JobId} was cancelled (service stopping)", job.Id);
                // Do not re-throw — let the foreach loop exit naturally via stoppingToken
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync job {JobId} failed unexpectedly", job.Id);
                // Do not crash the service — await next job
            }
        }

        _logger.LogInformation("Sync background service stopped");
    }
}
