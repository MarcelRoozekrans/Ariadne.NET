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
    /// or uniqueNames rule is found when its <c>from</c> matches a source type, and a slices rule when its patterns place a source type in a slice. A bannedPackages rule is found when its
    /// <c>from.assemblies</c> is empty or selects a project. A role is found when a role match holds it. An element marked
    /// <c>allowEmpty</c> is skipped, as is a rule whose <c>from</c> names only vacuous layers and a role in a vacuous layer,
    /// since the layer is reported instead. A forbid, allowOnly or uniqueNames rule with <c>requireTarget</c> also has its
    /// target checked, whatever <c>allowEmpty</c> says: its <c>to</c> is found when it matches a source type or an edge
    /// target, or a member edge when it names <c>members</c>, and a uniqueNames <c>against</c> when it matches a source type.
    /// A target that names only vacuous layers is skipped. A rule gives its subject violation first and its target one second.
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
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        var vacuous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            if (!rule.AllowEmpty && !NamesOnlyVacuousLayers(rule.From, vacuousLayers) && IsVacuous(rule, map, graphs, projects))
            {
                violations.Add(VacuousRule(rule, string.Create(CultureInfo.InvariantCulture, $"Rule '{rule.Id}' selects no type: {Cause(rule, count)}.")));
            }

            if (rule.RequireTarget
                && TargetOf(rule) is { } target
                && !NamesOnlyVacuousLayers(target, vacuousLayers)
                && !IsTargetFound(rule.Kind, target, map, graphs))
            {
                violations.Add(VacuousRule(rule, TargetCause(rule, target, count)));
            }
        }
    }

    private static ArchitectureViolation VacuousRule(RuleDefinition rule, string message) => new()
    {
        RuleId = rule.Id,
        Kind = ViolationKind.VacuousRule,
        SourceType = rule.Id,
        Because = rule.Because,
        Message = message,
    };

    // Only these kinds have a target; the loader and the validator reject requireTarget on the others.
    private static TargetSelector? TargetOf(RuleDefinition rule) => rule.Kind switch
    {
        RuleKind.Forbid or RuleKind.AllowOnly => rule.To,
        RuleKind.UniqueNames => rule.Against,
        _ => null,
    };

    private static string TargetCause(RuleDefinition rule, TargetSelector target, int count) =>
        rule.Kind == RuleKind.UniqueNames
            ? string.Create(CultureInfo.InvariantCulture, $"Rule '{rule.Id}' requires its target, but its 'against' selector matches no type declared in the {count} project(s) loaded.")
            : target.Members.Count > 0
                ? string.Create(CultureInfo.InvariantCulture, $"Rule '{rule.Id}' requires its target, but no code in the {count} project(s) loaded uses a member its 'to' selector names.")
                : string.Create(CultureInfo.InvariantCulture, $"Rule '{rule.Id}' requires its target, but its 'to' selector matches no type declared or referenced in the {count} project(s) loaded.");

    // uniqueNames compares declared types only, so its target is found by a source type alone. A members target is found by a
    // member edge only, since it selects no type.
    private static bool IsTargetFound(RuleKind kind, TargetSelector target, LayerMap map, IReadOnlyList<TypeGraph> graphs)
    {
        var matcher = TargetMatcher.From(target, map);
        foreach (var graph in graphs)
        {
            if (matcher.HasMembers && kind != RuleKind.UniqueNames)
            {
                foreach (var edge in graph.MemberEdges)
                {
                    if (matcher.MatchesMember(in edge))
                    {
                        return true;
                    }
                }

                continue;
            }

            foreach (var type in graph.SourceTypes)
            {
                if (matcher.Matches(type))
                {
                    return true;
                }
            }

            if (kind == RuleKind.UniqueNames)
            {
                continue;
            }

            foreach (var edge in graph.Edges)
            {
                if (matcher.Matches(edge.Target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Cause(RuleDefinition rule, int count) => rule.Kind switch
    {
        RuleKind.BannedPackages => string.Create(CultureInfo.InvariantCulture, $"its 'from.assemblies' matches none of the {count} project(s) loaded"),
        RuleKind.Slices => string.Create(CultureInfo.InvariantCulture, $"its 'matching' patterns place no type in a slice in the {count} project(s) loaded"),
        RuleKind.Require or RuleKind.UniqueNames => string.Create(CultureInfo.InvariantCulture, $"its 'for' selector matches nothing in the {count} project(s) loaded"),
        _ => string.Create(CultureInfo.InvariantCulture, $"its 'from' selector matches nothing in the {count} project(s) loaded"),
    };

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
        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        // A slices rule selects through its patterns and leaves 'from' empty; one slice is enough, though it cannot fail.
        if (rule.Kind == RuleKind.Slices)
        {
            return !SliceRules.PlacesAnyType(rule, graphs);
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
