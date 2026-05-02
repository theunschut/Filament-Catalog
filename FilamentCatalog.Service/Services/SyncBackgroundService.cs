using System.Threading.Channels;

public record SyncJob(int Id = 0);

public class SyncBackgroundService : BackgroundService
{
    private readonly Channel<SyncJob> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(
        Channel<SyncJob> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<SyncBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
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
                await using var scope = _scopeFactory.CreateAsyncScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                await syncService.SyncCatalogAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Sync job {JobId} was cancelled (service stopping)", job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync job {JobId} failed unexpectedly", job.Id);
            }
        }

        _logger.LogInformation("Sync background service stopped");
    }
}
