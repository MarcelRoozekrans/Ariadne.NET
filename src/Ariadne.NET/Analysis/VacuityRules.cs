using System.Globalization;
using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Finds the layers, rules and component roles of a pack that select nothing in the projects loaded. Such an element cannot
/// fail, so a clean report would say nothing about it.
/// </summary>
internal static class VacuityRules
{
    /// <summary>
    /// Returns one violation per vacuous layer, rule and role, without a location. A layer is found when it selects any source
    /// type or edge target of any graph; a layer with no namespaces and assemblies is never found. A forbid, allowOnly, require
    /// or uniqueNames rule is found when its <c>from</c> matches a source type. A bannedPackages rule is found when its
    /// <c>from.assemblies</c> is empty or selects a project. A role is found when a role match holds it. An element marked
    /// <c>allowEmpty</c> is skipped, as is a rule whose <c>from</c> names only vacuous layers and a role in a vacuous layer,
    /// since the layer is reported instead.
    /// </summary>
    /// <param name="pack">The resolved pack.</param>
    /// <param name="layers">The layer map of the pack.</param>
    /// <param name="graphs">The type graphs of the run.</param>
    /// <param name="roleMatches">The role matches of the run.</param>
    /// <param name="projects">The projects of the run.</param>
    /// <returns>The violations: layers, then rules, then roles, each in pack order.</returns>
    public static IReadOnlyList<ArchitectureViolation> Check(
        RulePack pack, LayerMap layers, IReadOnlyList<TypeGraph> graphs, IReadOnlyList<RoleMatches> roleMatches, IReadOnlyList<AnalyzedProject> projects)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(graphs);
        ArgumentNullException.ThrowIfNull(roleMatches);
        ArgumentNullException.ThrowIfNull(projects);

        var count = projects.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count();
        var violations = new List<ArchitectureViolation>();
        var vacuousLayers = CheckLayers(pack, layers, graphs, count, violations);
        CheckRules(pack, layers, graphs, projects, vacuousLayers, count, violations);
        CheckRoles(pack, roleMatches, vacuousLayers, count, violations);
        return violations;
    }

    private static HashSet<string> CheckLayers(RulePack pack, LayerMap map, IReadOnlyList<TypeGraph> graphs, int count, List<ArchitectureViolation> violations)
    {
        // A layer counts via LayersOf, so an overlapping type counts for each of its layers, while TargetMatcher.Matches uses
        // LayerOf, where a type in two layers is in none. A rule from such a layer can still be vacuous; LayerOverlap is reported separately.
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var graph in graphs)
        {
            foreach (var type in graph.SourceTypes)
            {
                found.UnionWith(map.LayersOf(type));
            }

            foreach (var edge in graph.Edges)
            {
                found.UnionWith(map.LayersOf(edge.Target));
            }
        }

        var vacuous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layer in pack.Layers)
        {
            if (found.Contains(layer.Name))
            {
                continue;
            }

            vacuous.Add(layer.Name);
            violations.Add(new ArchitectureViolation
            {
                RuleId = layer.Id.Length > 0 ? layer.Id : layer.Name,
                Kind = ViolationKind.VacuousLayer,
                SourceType = layer.Name,
                Because = layer.Because,
                Message = string.Create(CultureInfo.InvariantCulture, $"Layer '{layer.Name}' covers no type in the {count} project(s) loaded."),
            });
        }

        return vacuous;
    }

    private static void CheckRules(
        RulePack pack, LayerMap map, IReadOnlyList<TypeGraph> graphs, IReadOnlyList<AnalyzedProject> projects, HashSet<string> vacuousLayers, int count, List<ArchitectureViolation> violations)
    {
        foreach (var rule in pack.Rules)
        {
            if (rule.AllowEmpty || NamesOnlyVacuousLayers(rule.From, vacuousLayers) || !IsVacuous(rule, map, graphs, projects))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation
            {
                RuleId = rule.Id,
                Kind = ViolationKind.VacuousRule,
                SourceType = rule.Id,
                Because = rule.Because,
                Message = string.Create(CultureInfo.InvariantCulture, $"Rule '{rule.Id}' selects no type: its 'from' selector matches nothing in the {count} project(s) loaded."),
            });
        }
    }

    private static void CheckRoles(RulePack pack, IReadOnlyList<RoleMatches> roleMatches, HashSet<string> vacuousLayers, int count, List<ArchitectureViolation> violations)
    {
        HashSet<string>? held = null;
        foreach (var role in pack.ComponentRoles)
        {
            if (role.AllowEmpty || vacuousLayers.Contains(role.Layer))
            {
                continue;
            }

            held ??= HeldRoles(roleMatches);
            if (held.Contains(role.Name))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation
            {
                RuleId = role.Name,
                Kind = ViolationKind.VacuousRule,
                SourceType = role.Name,
                Message = string.Create(CultureInfo.InvariantCulture, $"Role '{role.Name}' matches no type in layer '{role.Layer}' in the {count} project(s) loaded."),
            });
        }
    }

    private static HashSet<string> HeldRoles(IReadOnlyList<RoleMatches> roleMatches)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (var matches in roleMatches)
        {
            foreach (var (_, role) in matches.Items)
            {
                held.Add(role.Name);
            }
        }

        return held;
    }

    private static bool NamesOnlyVacuousLayers(TargetSelector from, HashSet<string> vacuousLayers)
    {
        if (from.Layers.Count == 0)
        {
            return false;
        }

        foreach (var name in from.Layers)
        {
            if (!vacuousLayers.Contains(name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVacuous(RuleDefinition rule, LayerMap map, IReadOnlyList<TypeGraph> graphs, IReadOnlyList<AnalyzedProject> projects)
    {
        if (rule.Kind == RuleKind.BannedPackages)
        {
            var compiled = BannedPackageRules.CompiledRule.From(rule);
            if (compiled.FromAssemblies.Length == 0)
            {
                return false;
            }

            foreach (var project in projects)
            {
                if (compiled.SelectsAssembly(BannedPackageRules.AssemblyOf(project)))
                {
                    return false;
                }
            }

            return true;
        }

        var from = TargetMatcher.From(rule.From, map);
        foreach (var graph in graphs)
        {
            foreach (var type in graph.SourceTypes)
            {
                if (from.Matches(type))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
