using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleSchema;

using ZInject;

namespace Ariadne.NET.RuleLoader;

[Singleton]
public sealed class RulePackResolver : IRulePackResolver
{
    private const string BuiltinPrefix = "builtin://";
    private const string ImportPrefix = "import://";

    private readonly IBuiltinPackRegistry _registry;
    private readonly IRulePackLoader _loader;
    private readonly IRulePackMerger _merger;

    public RulePackResolver(IBuiltinPackRegistry registry, IRulePackLoader loader, IRulePackMerger merger)
    {
        _registry = registry;
        _loader = loader;
        _merger = merger;
    }

    public RulePack Resolve(RulePack userPack) => Resolve(userPack, workingDirectory: null);

    public RulePack Resolve(RulePack userPack, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(userPack);

        if (userPack.Extends.Count == 0)
        {
            return userPack;
        }

        var packs = new List<RulePack>();

        foreach (var reference in userPack.Extends)
        {
            if (reference.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var packName = reference[BuiltinPrefix.Length..];
                var pack = _registry.GetPack(packName);
                packs.Add(pack);
            }
            else if (reference.StartsWith(ImportPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var importName = reference[ImportPrefix.Length..];
                var pack = LoadImportedPack(importName, workingDirectory);
                packs.Add(pack);
            }
            else
            {
                throw new NotSupportedException($"Unsupported extends reference: '{reference}'. Only 'builtin://' and 'import://' references are currently supported.");
            }
        }

        packs.Add(userPack);

        return _merger.Merge(packs);
    }

    private RulePack LoadImportedPack(string importName, string? workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            throw new InvalidOperationException(
                $"Cannot resolve import '{importName}' without a working directory. Run 'archgov sync' first to fetch imported packs.");
        }

        var cacheDir = Path.Combine(workingDirectory, ".architecture", ".cache", "git", importName);

        if (!Directory.Exists(cacheDir))
        {
            throw new InvalidOperationException(
                $"Cache directory for import '{importName}' not found at '{cacheDir}'. Run 'archgov sync' to fetch imported packs.");
        }

        var yamlFiles = Directory.GetFiles(cacheDir, "*.yaml", SearchOption.TopDirectoryOnly);

        if (yamlFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No YAML files found in cache directory '{cacheDir}' for import '{importName}'. Run 'archgov sync' to fetch imported packs.");
        }

        return _loader.LoadFromFile(yamlFiles[0]);
    }
}
