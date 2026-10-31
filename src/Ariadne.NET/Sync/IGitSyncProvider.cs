namespace Ariadne.NET.Sync;

public interface IGitSyncProvider
{
    Task<SyncResult> SyncAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct = default);
}
