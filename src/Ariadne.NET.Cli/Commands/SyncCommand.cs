using System.CommandLine;
using Ariadne.NET.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

internal static class SyncCommand
{
    private const string DefaultImportsPath = ".architecture/imports.yaml";
    private const string DefaultCacheDir = ".architecture/.cache/git";

    public static Command Create(IServiceProvider provider)
    {
        var noCacheOption = new Option<bool>("--no-cache")
        {
            Description = "Force fresh sync, ignore cached versions",
        };

        var nameOption = new Option<string?>("--name", "-n")
        {
            Description = "Sync only the import with this name",
        };

        var command = new Command("sync", "Sync rule packs from git repositories defined in imports.yaml")
        {
            noCacheOption,
            nameOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var noCache = parseResult.GetValue(noCacheOption);
            var name = parseResult.GetValue(nameOption);
            return await ExecuteAsync(provider, name, noCache, ct).ConfigureAwait(false);
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        IServiceProvider provider, string? name, bool noCache, CancellationToken ct)
    {
        var workingDir = Directory.GetCurrentDirectory();
        var importsPath = Path.Combine(workingDir, DefaultImportsPath);

        var loader = provider.GetRequiredService<IImportsLoader>();
        var orchestrator = provider.GetRequiredService<ISyncOrchestrator>();

        // A missing file is the loader's NotFound, with the path as Detail.
        var loaded = loader.LoadFromFile(importsPath);
        if (loaded.IsFailure)
        {
            return ErrorOutput.Report(loaded.Error);
        }

        var manifest = loaded.Value;

        if (manifest.Imports.Count == 0)
        {
            Console.WriteLine("No imports defined in imports.yaml.");
            return ExitCodes.Success;
        }

        var cacheDir = Path.Combine(workingDir, DefaultCacheDir);
        Directory.CreateDirectory(cacheDir);

        IReadOnlyList<ImportSyncOutcome> outcomes;

        if (!string.IsNullOrEmpty(name))
        {
            Console.WriteLine($"Syncing import '{name}'...");
            outcomes = await orchestrator.SyncByNameAsync(manifest, name, cacheDir, noCache, ct)
                .ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine($"Syncing {manifest.Imports.Count} import(s)...");
            outcomes = await orchestrator.SyncAllAsync(manifest, cacheDir, noCache, ct)
                .ConfigureAwait(false);
        }

        PrintOutcomes(outcomes);
        return outcomes.All(o => o.Result.IsSuccess) ? ExitCodes.Success : ExitCodes.Error;
    }

    private static void PrintOutcomes(IReadOnlyList<ImportSyncOutcome> outcomes)
    {
        var successCount = 0;
        var failCount = 0;

        foreach (var outcome in outcomes)
        {
            if (outcome.Result.IsSuccess)
            {
                var synced = outcome.Result.Value;
                var status = synced.WasCached ? "cached" : "fetched";
                Console.WriteLine($"  {synced.ImportName,-30} {synced.ResolvedVersion,-12} ({status})");
                successCount++;
            }
            else
            {
                var error = outcome.Result.Error;
                Console.Error.WriteLine($"  {outcome.ImportName,-30} FAILED: {error.Message}");
                if (error.Detail is not null)
                {
                    Console.Error.WriteLine($"    {error.Detail}");
                }

                failCount++;
            }
        }

        Console.WriteLine();
        if (failCount == 0)
        {
            Console.WriteLine($"Done. {successCount} pack(s) synced.");
        }
        else
        {
            Console.Error.WriteLine($"Done. {successCount} synced, {failCount} failed.");
        }
    }
}
