using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

public interface IGitSyncProvider
{
    Task<Result<SyncedImport, AriadneError>> SyncAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct = default);
}
