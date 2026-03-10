namespace ArchGov.Core.Sync;

public interface ISyncOrchestrator
{
    Task<IReadOnlyList<SyncResult>> SyncAllAsync(ImportsManifest manifest, string cacheDir, bool noCache, CancellationToken ct = default);
    Task<IReadOnlyList<SyncResult>> SyncByNameAsync(ImportsManifest manifest, string name, string cacheDir, bool noCache, CancellationToken ct = default);
}
