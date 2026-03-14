using ZInject;

namespace ArchGov.Core.Sync;

[Singleton]
public sealed class SyncOrchestrator(IGitSyncProvider gitProvider) : ISyncOrchestrator
{
    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(ImportsManifest manifest, string cacheDir, bool noCache, CancellationToken ct = default)
    {
        var results = new List<SyncResult>();
        foreach (var import in manifest.Imports)
        {
            results.Add(await SyncSingleAsync(import, cacheDir, noCache, ct).ConfigureAwait(false));
        }
        return results;
    }

    public async Task<IReadOnlyList<SyncResult>> SyncByNameAsync(ImportsManifest manifest, string name, string cacheDir, bool noCache, CancellationToken ct = default)
    {
        var import = manifest.Imports.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (import is null)
        {
            return
            [
                new SyncResult
                {
                    ImportName = name,
                    Success = false,
                    Error = $"Import '{name}' not found in manifest."
                }
            ];
        }

        var result = await SyncSingleAsync(import, cacheDir, noCache, ct).ConfigureAwait(false);
        return [result];
    }

    private async Task<SyncResult> SyncSingleAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct)
    {
        if (!string.Equals(import.Type, "git", StringComparison.OrdinalIgnoreCase))
        {
            return new SyncResult
            {
                ImportName = import.Name,
                Success = false,
                Error = $"Unsupported import type '{import.Type}'. Only 'git' is currently supported."
            };
        }

        return await gitProvider.SyncAsync(import, cacheDir, noCache, ct).ConfigureAwait(false);
    }
}
