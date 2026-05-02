public interface ISyncService
{
    Task SyncCatalogAsync(CancellationToken cancellationToken);
}
