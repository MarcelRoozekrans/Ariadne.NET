using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleLoader;

public sealed class RulePackResolver : IRulePackResolver
{
    private const string BuiltinPrefix = "builtin://";

    private readonly IBuiltinPackRegistry _registry;
    private readonly IRulePackMerger _merger;

    public RulePackResolver(IBuiltinPackRegistry registry, IRulePackLoader loader, IRulePackMerger merger)
    {
        _registry = registry;
        _ = loader; // Reserved for future use (e.g., file:// or http:// resolution)
        _merger = merger;
    }

    public RulePack Resolve(RulePack userPack)
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
            else
            {
                throw new NotSupportedException($"Unsupported extends reference: '{reference}'. Only 'builtin://' references are currently supported.");
            }
        }

        packs.Add(userPack);

        return _merger.Merge(packs);
    }
}
