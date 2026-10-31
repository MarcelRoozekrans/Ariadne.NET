using Ariadne.NET.Abstractions;
using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

[Singleton]
public sealed class SyncOrchestrator(IGitSyncProvider gitProvider) : ISyncOrchestrator
{
    public async Task<IReadOnlyList<ImportSyncOutcome>> SyncAllAsync(ImportsManifest manifest, string cacheDir, bool noCache, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var outcomes = new List<ImportSyncOutcome>(manifest.Imports.Count);
        foreach (var import in manifest.Imports)
        {
            outcomes.Add(await SyncSingleAsync(import, cacheDir, noCache, ct).ConfigureAwait(false));
        }

        return outcomes;
    }

    public async Task<IReadOnlyList<ImportSyncOutcome>> SyncByNameAsync(ImportsManifest manifest, string name, string cacheDir, bool noCache, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var import = manifest.Imports.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (import is null)
        {
            return
            [
                new ImportSyncOutcome(
                    name,
                    Result<SyncedImport, AriadneError>.Failure(new AriadneError(
                        AriadneErrorCode.NotFound,
                        $"Import '{name}' not found in manifest."))),
            ];
        }

        return [await SyncSingleAsync(import, cacheDir, noCache, ct).ConfigureAwait(false)];
    }

    private async Task<ImportSyncOutcome> SyncSingleAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct)
    {
        var importName = import.Name ?? string.Empty;
        if (!string.Equals(import.Type, "git", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSyncOutcome(
                importName,
                Result<SyncedImport, AriadneError>.Failure(new AriadneError(
                    AriadneErrorCode.Invalid,
                    $"Unsupported import type '{import.Type}'. Only 'git' is currently supported.")));
        }

        var result = await gitProvider.SyncAsync(import, cacheDir, noCache, ct).ConfigureAwait(false);
        return new ImportSyncOutcome(importName, result);
    }
}
