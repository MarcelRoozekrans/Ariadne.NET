using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

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

    public Result<RulePack, AriadneError> Resolve(RulePack userPack) => Resolve(userPack, workingDirectory: null);

    public Result<RulePack, AriadneError> Resolve(RulePack userPack, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(userPack);

        if (userPack.Extends.Count == 0)
        {
            return Result<RulePack, AriadneError>.Success(userPack);
        }

        var packs = new List<RulePack>();

        // Stop at the first reference that fails: the user fixes it and runs again.
        foreach (var reference in userPack.Extends)
        {
            var pack = LoadReference(reference, workingDirectory);
            if (pack.IsFailure)
            {
                return pack;
            }

            packs.Add(pack.Value);
        }

        packs.Add(userPack);

        return Result<RulePack, AriadneError>.Success(_merger.Merge(packs));
    }

    private Result<RulePack, AriadneError> LoadReference(string reference, string? workingDirectory)
    {
        if (reference.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return _registry.GetPack(reference[BuiltinPrefix.Length..]);
        }

        if (reference.StartsWith(ImportPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return LoadImportedPack(reference[ImportPrefix.Length..], workingDirectory);
        }

        return Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.Invalid,
            $"Unsupported extends reference: '{reference}'. Only 'builtin://' and 'import://' references are currently supported."));
    }

    private Result<RulePack, AriadneError> LoadImportedPack(string importName, string? workingDirectory)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"Cannot resolve import '{importName}' without a working directory. Run 'ariadne sync' first to fetch imported packs."));
        }

        var cacheDir = Path.Combine(workingDirectory, ".architecture", ".cache", "git", importName);

        if (!Directory.Exists(cacheDir))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"Cache directory for import '{importName}' not found. Run 'ariadne sync' to fetch imported packs.",
                cacheDir));
        }

        var yamlFiles = Directory.GetFiles(cacheDir, "*.yaml", SearchOption.TopDirectoryOnly);

        if (yamlFiles.Length == 0)
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"No YAML files found in the cache directory for import '{importName}'. Run 'ariadne sync' to fetch imported packs.",
                cacheDir));
        }

        return _loader.LoadFromFile(yamlFiles[0]);
    }
}
