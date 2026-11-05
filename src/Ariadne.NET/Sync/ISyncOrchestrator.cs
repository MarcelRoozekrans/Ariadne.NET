namespace Ariadne.NET.Sync;

public interface ISyncOrchestrator
{
    Task<IReadOnlyList<ImportSyncOutcome>> SyncAllAsync(ImportsManifest manifest, string cacheDir, bool noCache, CancellationToken ct = default);
    Task<IReadOnlyList<ImportSyncOutcome>> SyncByNameAsync(ImportsManifest manifest, string name, string cacheDir, bool noCache, CancellationToken ct = default);
}
