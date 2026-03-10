using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public sealed class RulePackMerger : IRulePackMerger
{
    public RulePack Merge(IReadOnlyList<RulePack> packs)
    {
        return MergeWithDiagnostics(packs).MergedPack;
    }

    public MergeResult MergeWithDiagnostics(IReadOnlyList<RulePack> packs)
    {
        ArgumentNullException.ThrowIfNull(packs);

        if (packs.Count == 0)
        {
            return new MergeResult
            {
                MergedPack = new RulePack { Name = "" },
                Warnings = [],
            };
        }

        var warnings = new List<MergeWarning>();
        var mergedName = string.Join("+", packs.Select(p => p.Name));
        var mergedLayers = MergeLayers(packs);
        var mergedRoles = MergeComponentRoles(packs);
        var mergedFeatureRules = MergeCodeFeatureRules(packs, warnings);
        var mergedNaming = packs[^1].NamingConventions;

        var mergedPack = new RulePack
        {
            Name = mergedName,
            Layers = mergedLayers,
            ComponentRoles = mergedRoles,
            CodeFeatureRules = mergedFeatureRules,
            NamingConventions = mergedNaming,
        };

        return new MergeResult
        {
            MergedPack = mergedPack,
            Warnings = warnings,
        };
    }

    private static IList<LayerDefinition> MergeLayers(IReadOnlyList<RulePack> packs)
    {
        var layerMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var layerDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in packs)
        {
            foreach (var layer in pack.Layers)
            {
                if (!layerMap.TryGetValue(layer.Name, out var deps))
                {
                    deps = [];
                    layerMap[layer.Name] = deps;
                }

                foreach (var dep in layer.AllowedDependencies)
                {
                    if (!deps.Contains(dep, StringComparer.OrdinalIgnoreCase))
                    {
                        deps.Add(dep);
                    }
                }

                layerDescriptions[layer.Name] = layer.Description;
            }
        }

        return layerMap.Select(kvp => new LayerDefinition
        {
            Name = kvp.Key,
            Description = layerDescriptions[kvp.Key],
            AllowedDependencies = kvp.Value.ToList(),
        }).ToList();
    }

    private static IList<ComponentRoleDefinition> MergeComponentRoles(IReadOnlyList<RulePack> packs)
    {
        var roleMap = new Dictionary<string, ComponentRoleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in packs)
        {
            foreach (var role in pack.ComponentRoles)
            {
                roleMap[role.Name] = role;
            }
        }

        return roleMap.Values.ToList();
    }

    private static IList<CodeFeatureRuleDefinition> MergeCodeFeatureRules(
        IReadOnlyList<RulePack> packs,
        List<MergeWarning> warnings)
    {
        var ruleMap = new Dictionary<string, CodeFeatureRuleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in packs)
        {
            foreach (var rule in pack.CodeFeatureRules)
            {
                if (ruleMap.TryGetValue(rule.Id, out var existing) && existing.Locked)
                {
                    warnings.Add(new MergeWarning(
                        $"Rule '{rule.Id}' is locked and cannot be overridden by pack '{pack.Name}'."));
                    continue;
                }

                ruleMap[rule.Id] = rule;
            }
        }

        return ruleMap.Values.ToList();
    }
}
