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

        if (!File.Exists(importsPath))
        {
            Console.Error.WriteLine($"No imports file found at {importsPath}");
            Console.Error.WriteLine("Create .architecture/imports.yaml to define external rule packs.");
            return ExitCodes.Error;
        }

        var loader = provider.GetRequiredService<IImportsLoader>();
        var orchestrator = provider.GetRequiredService<ISyncOrchestrator>();

        var manifest = loader.LoadFromFile(importsPath);

        if (manifest.Imports.Count == 0)
        {
            Console.WriteLine("No imports defined in imports.yaml.");
            return ExitCodes.Success;
        }

        var cacheDir = Path.Combine(workingDir, DefaultCacheDir);
        Directory.CreateDirectory(cacheDir);

        IReadOnlyList<SyncResult> results;

        if (!string.IsNullOrEmpty(name))
        {
            Console.WriteLine($"Syncing import '{name}'...");
            results = await orchestrator.SyncByNameAsync(manifest, name, cacheDir, noCache, ct)
                .ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine($"Syncing {manifest.Imports.Count} import(s)...");
            results = await orchestrator.SyncAllAsync(manifest, cacheDir, noCache, ct)
                .ConfigureAwait(false);
        }

        PrintResults(results);
        return results.All(r => r.Success) ? ExitCodes.Success : ExitCodes.Error;
    }

    private static void PrintResults(IReadOnlyList<SyncResult> results)
    {
        var successCount = 0;
        var failCount = 0;

        foreach (var result in results)
        {
            if (result.Success)
            {
                var status = result.WasCached ? "cached" : "fetched";
                Console.WriteLine($"  {result.ImportName,-30} {result.ResolvedVersion,-12} ({status})");
                successCount++;
            }
            else
            {
                Console.Error.WriteLine($"  {result.ImportName,-30} FAILED: {result.Error}");
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
