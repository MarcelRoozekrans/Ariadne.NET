using Ariadne.NET.Abstractions;
using Ariadne.NET.Telemetry;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>Syncs an import with the git CLI; each call opens one <c>ariadne.sync_import</c> span, retries included.</summary>
[Singleton]
internal sealed class GitCliSyncProvider : IGitSyncProvider
{
    private readonly IGitClient _git;

    /// <summary>Takes the git client the container builds: the resilience proxy around <see cref="GitCliClient"/>.</summary>
    public GitCliSyncProvider(IGitClient git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public Task<Result<SyncedImport, AriadneError>> SyncAsync(
        ImportDefinition import,
        string cacheDir,
        bool noCache,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(cacheDir);

        return SyncOperations.Instrumented.SyncImportAsync(import, cacheDir, noCache, _git, ct);
    }
}
