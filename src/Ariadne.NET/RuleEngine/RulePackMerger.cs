using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.RuleEngine;

[Singleton]
public sealed class RulePackMerger : IRulePackMerger
{
    public Result<RulePack, AriadneError> Merge(IReadOnlyList<RulePack> packs)
    {
        ArgumentNullException.ThrowIfNull(packs);

        if (packs.Count == 0)
        {
            return Result<RulePack, AriadneError>.Success(new RulePack { Name = "" });
        }

        var mergedName = string.Join("+", packs.Select(static p => p.Name));
        var problems = new List<string>();
        var layers = new Owned<LayerDefinition>();
        var rules = new Owned<RuleDefinition>();
        var roles = new Dictionary<string, ComponentRoleDefinition>(StringComparer.OrdinalIgnoreCase);

        for (var p = 0; p < packs.Count; p++)
        {
            var pack = packs[p];

            // Retargets and amendments see only what earlier packs defined.
            ApplyRetargets(pack, layers, problems);
            ApplyAmendments(pack, rules, problems);

            AddDefinitions(pack, p, layers, rules, problems);

            foreach (var role in pack.ComponentRoles)
            {
                roles[role.Name] = role;
            }
        }

        if (problems.Count > 0)
        {
            return Result<RulePack, AriadneError>.Failure(RulePackError.Invalid(mergedName, problems));
        }

        return Result<RulePack, AriadneError>.Success(new RulePack
        {
            Name = mergedName,
            Schema = 2,
            Layers = layers.Values(),
            ComponentRoles = roles.Values.ToList(),
            Rules = rules.Values(),
            NamingConventions = packs[^1].NamingConventions,
        });
    }

    private static void AddDefinitions(
        RulePack pack, int packIndex, Owned<LayerDefinition> layers, Owned<RuleDefinition> rules, List<string> problems)
    {
        foreach (var layer in pack.Layers)
        {
            if (layers.TryGet(layer.Name, out var existing) && existing.PackIndex != packIndex)
            {
                problems.Add($"packs '{PackText.Show(existing.PackName)}' and '{PackText.Show(pack.Name)}' both define layer '{PackText.Show(layer.Name)}'");
                continue;
            }

            layers.Add(layer.Name, layer, packIndex, pack.Name);
        }

        foreach (var rule in pack.Rules)
        {
            if (rules.TryGet(rule.Id, out var existing) && existing.PackIndex != packIndex)
            {
                problems.Add($"packs '{PackText.Show(existing.PackName)}' and '{PackText.Show(pack.Name)}' both define rule '{PackText.Show(rule.Id)}'");
                continue;
            }

            rules.Add(rule.Id, rule, packIndex, pack.Name);
        }
    }

    private static void ApplyRetargets(RulePack pack, Owned<LayerDefinition> layers, List<string> problems)
    {
        foreach (var retarget in pack.LayerRetargets)
        {
            if (!layers.TryGet(retarget.Name, out var target))
            {
                problems.Add($"pack '{PackText.Show(pack.Name)}' retargets layer '{PackText.Show(retarget.Name)}', which no extended pack defines");
                continue;
            }

            var inherited = target.Item;
            layers.Replace(retarget.Name, new LayerDefinition
            {
                Name = inherited.Name,
                Id = inherited.Id,
                Because = inherited.Because,
                Description = inherited.Description,
                AllowedDependencies = inherited.AllowedDependencies,
                Namespaces = retarget.Namespaces,
                Assemblies = retarget.Assemblies,
            });
        }
    }

    private static void ApplyAmendments(RulePack pack, Owned<RuleDefinition> rules, List<string> problems)
    {
        foreach (var amendment in pack.RuleAmendments)
        {
            if (!rules.TryGet(amendment.Id, out var target))
            {
                problems.Add($"pack '{PackText.Show(pack.Name)}' amends rule '{PackText.Show(amendment.Id)}', which no extended pack defines");
                continue;
            }

            var inherited = target.Item;
            if (inherited.Locked)
            {
                problems.Add($"rule '{PackText.Show(inherited.Id)}' from pack '{PackText.Show(target.PackName)}' is locked; pack '{PackText.Show(pack.Name)}' cannot add exceptions");
                continue;
            }

            rules.Replace(amendment.Id, new RuleDefinition
            {
                Id = inherited.Id,
                Because = inherited.Because,
                Description = inherited.Description,
                Kind = inherited.Kind,
                From = inherited.From,
                To = inherited.To,
                Require = inherited.Require,
                Exceptions = [.. inherited.Exceptions, .. amendment.Exceptions],
                Locked = inherited.Locked,
            });
        }
    }

    private sealed record Entry<T>(T Item, int PackIndex, string PackName);

    /// <summary>Items by case-insensitive key, in first-defined order, each with the pack that defined it.</summary>
    private sealed class Owned<T>
    {
        private readonly Dictionary<string, int> _indexByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Entry<T>> _ordered = [];

        public bool TryGet(string key, out Entry<T> entry)
        {
            if (_indexByKey.TryGetValue(key, out var index))
            {
                entry = _ordered[index];
                return true;
            }

            entry = null!;
            return false;
        }

        public void Add(string key, T item, int packIndex, string packName)
        {
            _indexByKey.TryAdd(key, _ordered.Count);
            _ordered.Add(new Entry<T>(item, packIndex, packName));
        }

        // Keeps the defining pack: a retarget or amendment changes the item, not who owns it.
        public void Replace(string key, T item)
        {
            var index = _indexByKey[key];
            _ordered[index] = _ordered[index] with { Item = item };
        }

        public List<T> Values() => _ordered.Select(static e => e.Item).ToList();
    }
}
