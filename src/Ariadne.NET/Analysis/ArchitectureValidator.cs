using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;
using ZeroAlloc.Inject;

namespace Ariadne.NET.Analysis;

/// <summary>Builds the type graph of each project and target framework, runs the layer, forbid, allowOnly, require and role rules and merges the results per project.</summary>
[Singleton]
public sealed class ArchitectureValidator : IArchitectureValidator
{
    /// <inheritdoc />
    public ArchitectureReport Validate(RulePack resolvedPack, Compilation compilation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return Validate(resolvedPack, [new AnalyzedProject(compilation.AssemblyName ?? string.Empty, null, compilation)], nameof(compilation), cancellationToken);
    }

    /// <inheritdoc />
    public ArchitectureReport Validate(RulePack resolvedPack, IReadOnlyList<AnalyzedProject> projects, CancellationToken cancellationToken = default) =>
        Validate(resolvedPack, projects, nameof(projects), cancellationToken);

    // paramName is the argument the caller passed, so a guard failure names a parameter of the overload that was called.
    private ArchitectureReport Validate(RulePack resolvedPack, IReadOnlyList<AnalyzedProject> projects, string paramName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(projects);

        RejectMixedTargetFrameworks(projects);
        var bannedPackages = BannedPackageRules.Compile(resolvedPack);
        bannedPackages.RequirePackages(projects, paramName);

        // The layer map and the matchers depend on the pack only, so one set serves every project, and a type's layer,
        // names and matches are worked out once per run.
        var layers = new LayerMap(resolvedPack);
        var forbid = ForbidRules.Compile(layers, resolvedPack);
        var allowOnly = AllowOnlyRules.Compile(layers, resolvedPack);
        var require = RequireRules.Compile(layers, resolvedPack);

        var entries = new List<Entry>(projects.Count);
        foreach (var project in projects)
        {
            ArgumentNullException.ThrowIfNull(project, nameof(projects));
            cancellationToken.ThrowIfCancellationRequested();

            entries.Add(AnalyzeProject(project, layers, resolvedPack, new Matchers(forbid, allowOnly, require, bannedPackages), cancellationToken));
        }

        AddMissingInterfaceViolations(entries, resolvedPack);

        // Name collisions span projects, and a type seen in several target frameworks counts once, so they are found
        // once over all graphs and not merged per project.
        var nameCollisions = UniqueNameRules.Check(layers, resolvedPack, GraphsOf(entries));

        var ordered = GroupByProject(entries);
        var allViolations = new List<ArchitectureViolation>();
        var checkedProjects = new List<CheckedProject>(ordered.Count);
        var typesChecked = 0;
        foreach (ref readonly var group in CollectionsMarshal.AsSpan(ordered))
        {
            group.AddMergedViolations(allViolations);
            checkedProjects.Add(new CheckedProject(group.Name, group.TargetFrameworks()));
            typesChecked += group.TypesChecked;
        }

        allViolations.AddRange(nameCollisions);
        KeepOneOverlapPerType(allViolations);
        allViolations.Sort(CompareViolations);
        return new ArchitectureReport
        {
            Violations = allViolations,
            Projects = checkedProjects,
            TypesChecked = typesChecked,
        };
    }

    private static Entry AnalyzeProject(AnalyzedProject project, LayerMap layers, RulePack pack, in Matchers matchers, CancellationToken cancellationToken)
    {
        var graph = TypeGraphBuilder.Build(project.Compilation, project.GeneratedTrees, cancellationToken);
        var violations = new List<ArchitectureViolation>(LayerRules.Check(graph, layers, pack));
        violations.AddRange(matchers.Forbid.Check(graph));
        violations.AddRange(matchers.AllowOnly.Check(graph));
        violations.AddRange(matchers.Require.Check(graph));
        var matches = RoleRules.Match(graph, layers, pack);
        violations.AddRange(RoleRules.CheckShape(matches));
        violations.AddRange(matchers.BannedPackages.Check(project));
        return new Entry(project, graph, matches, violations);
    }

    private static List<TypeGraph> GraphsOf(List<Entry> entries)
    {
        var graphs = new List<TypeGraph>(entries.Count);
        foreach (ref readonly var entry in CollectionsMarshal.AsSpan(entries))
        {
            graphs.Add(entry.Graph);
        }

        return graphs;
    }

    // The entries of each project, in the order the projects first appear.
    private static List<ProjectGroup> GroupByProject(List<Entry> entries)
    {
        var groups = new Dictionary<string, ProjectGroup>(StringComparer.Ordinal);
        var ordered = new List<ProjectGroup>();
        foreach (ref readonly var entry in CollectionsMarshal.AsSpan(entries))
        {
            if (!groups.TryGetValue(entry.Project.Name, out var group))
            {
                group = new ProjectGroup(entry.Project.Name);
                groups.Add(group.Name, group);
                ordered.Add(group);
            }

            group.Add(entry);
        }

        return ordered;
    }

    // A type that several layers cover is found where it is declared, with its location, and again as a target in every
    // project that references it, without one. It is one problem: an unlocated overlap is dropped when the run holds a
    // located one for the same type, and kept once otherwise.
    private static void KeepOneOverlapPerType(List<ArchitectureViolation> violations)
    {
        var located = new HashSet<string>(StringComparer.Ordinal);
        foreach (ref readonly var violation in CollectionsMarshal.AsSpan(violations))
        {
            if (violation is { Kind: ViolationKind.LayerOverlap, Location: not null })
            {
                located.Add(violation.SourceType);
            }
        }

        var unlocated = new HashSet<string>(StringComparer.Ordinal);
        var kept = 0;
        for (var i = 0; i < violations.Count; i++)
        {
            var violation = violations[i];
            if (violation is { Kind: ViolationKind.LayerOverlap, Location: null }
                && (located.Contains(violation.SourceType) || !unlocated.Add(violation.SourceType)))
            {
                continue;
            }

            violations[kept++] = violation;
        }

        violations.RemoveRange(kept, violations.Count - kept);
    }

    // A violation found only in a null slot could not be told apart from one found everywhere, so a project is all null or all named.
    private static void RejectMixedTargetFrameworks(IReadOnlyList<AnalyzedProject> projects)
    {
        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            ArgumentNullException.ThrowIfNull(project, nameof(projects));

            var named = project.TargetFramework is not null;
            if (seen.TryGetValue(project.Name, out var previous))
            {
                if (previous != named)
                {
                    throw new ArgumentException(
                        $"Project '{project.Name}' has entries with and without a target framework; the target frameworks of one project must be all null or all named.",
                        nameof(projects));
                }
            }
            else
            {
                seen.Add(project.Name, named);
            }
        }
    }

    // A role that requires an interface role is satisfied by a matching type in any project of the same target framework.
    // A project with a null target framework is single-target, so it is present in every framework's scope, and its own
    // scope holds the matches of every project, because the checks are open-world. Two different named frameworks never
    // satisfy each other.
    private static void AddMissingInterfaceViolations(List<Entry> entries, RulePack pack)
    {
        var unnamed = new List<RoleMatches>();
        var all = new List<RoleMatches>(entries.Count);
        var byFramework = new Dictionary<string, List<RoleMatches>>(StringComparer.Ordinal);
        foreach (ref readonly var entry in CollectionsMarshal.AsSpan(entries))
        {
            all.Add(entry.Matches);
            if (entry.Project.TargetFramework is not { } framework)
            {
                unnamed.Add(entry.Matches);
                continue;
            }

            if (!byFramework.TryGetValue(framework, out var list))
            {
                list = [];
                byFramework.Add(framework, list);
            }

            list.Add(entry.Matches);
        }

        foreach (var list in byFramework.Values)
        {
            list.AddRange(unnamed);
        }

        foreach (ref readonly var entry in CollectionsMarshal.AsSpan(entries))
        {
            var scope = entry.Project.TargetFramework is { } framework ? byFramework[framework] : all;
            entry.Violations.AddRange(RoleRules.CheckMissingInterface(entry.Matches, scope, pack));
        }
    }

    // Located violations first, ordered by file path, line, column and rule id; the rest follow the same tie-breakers.
    private static int CompareViolations(ArchitectureViolation x, ArchitectureViolation y)
    {
        int result;
        switch (x.Location, y.Location)
        {
            case ({ } a, { } b):
                result = TypeGraphBuilder.Compare(a, b);
                if (result == 0)
                {
                    result = string.CompareOrdinal(x.RuleId, y.RuleId);
                }

                break;
            case ({ }, null):
                return -1;
            case (null, { }):
                return 1;
            default:
                result = string.CompareOrdinal(x.RuleId, y.RuleId);
                break;
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.SourceType, y.SourceType);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.Target, y.Target);
        }

        return result == 0 ? x.Kind.CompareTo(y.Kind) : result;
    }

    private readonly record struct Matchers(DependencyRuleChecker Forbid, DependencyRuleChecker AllowOnly, RequireRules.Checker Require, BannedPackageRules.Compiled BannedPackages);

    private sealed class Entry(AnalyzedProject project, TypeGraph graph, RoleMatches matches, List<ArchitectureViolation> violations)
    {
        public AnalyzedProject Project { get; } = project;

        public TypeGraph Graph { get; } = graph;

        public RoleMatches Matches { get; } = matches;

        public List<ArchitectureViolation> Violations { get; } = violations;
    }

    private readonly record struct ViolationKey(string RuleId, ViolationKind Kind, string SourceType, string? Target);

    private sealed class Merged(ArchitectureViolation violation)
    {
        public ArchitectureViolation Violation { get; set; } = violation;

        public List<string?> Frameworks { get; } = [];
    }

    private sealed class ProjectGroup(string name)
    {
        private readonly List<string?> _frameworks = [];
        private readonly Dictionary<ViolationKey, Merged> _merged = [];
        private readonly HashSet<string> _types = new(StringComparer.Ordinal);

        public string Name { get; } = name;

        public int TypesChecked => _types.Count;

        public void Add(Entry entry)
        {
            var framework = entry.Project.TargetFramework;
            if (!_frameworks.Contains(framework))
            {
                _frameworks.Add(framework);
            }

            foreach (var type in entry.Graph.SourceTypes)
            {
                _types.Add(type.ToDisplayString());
            }

            foreach (ref readonly var violation in CollectionsMarshal.AsSpan(entry.Violations))
            {
                var key = new ViolationKey(violation.RuleId, violation.Kind, violation.SourceType, violation.Target);
                if (!_merged.TryGetValue(key, out var merged))
                {
                    _merged.Add(key, merged = new Merged(violation));
                }
                else
                {
                    merged.Violation = Merge(merged.Violation, violation);
                }

                if (!merged.Frameworks.Contains(framework))
                {
                    merged.Frameworks.Add(framework);
                }
            }
        }

        public IReadOnlyList<string> TargetFrameworks()
        {
            var result = new List<string>(_frameworks.Count);
            foreach (ref readonly var framework in CollectionsMarshal.AsSpan(_frameworks))
            {
                if (framework is not null)
                {
                    result.Add(framework);
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public void AddMergedViolations(List<ArchitectureViolation> into)
        {
            foreach (var merged in _merged.Values)
            {
                if (merged.Frameworks.Count == _frameworks.Count)
                {
                    into.Add(merged.Violation);
                    continue;
                }

                var present = new List<string>(merged.Frameworks.Count);
                foreach (ref readonly var framework in CollectionsMarshal.AsSpan(merged.Frameworks))
                {
                    if (framework is not null)
                    {
                        present.Add(framework);
                    }
                }

                present.Sort(StringComparer.Ordinal);
                into.Add(merged.Violation with { TargetFrameworks = present });
            }
        }

        // The earlier location wins; the reference count is the highest, and the message is rebuilt to state that count.
        private static ArchitectureViolation Merge(ArchitectureViolation current, ArchitectureViolation candidate)
        {
            var count = Math.Max(current.ReferenceCount, candidate.ReferenceCount);
            var candidateIsEarlier = candidate.Location is { } c
                && (current.Location is not { } l || TypeGraphBuilder.Compare(c, l) < 0);
            return LayerRules.WithReferenceCount(candidateIsEarlier ? candidate : current, count);
        }
    }
}
