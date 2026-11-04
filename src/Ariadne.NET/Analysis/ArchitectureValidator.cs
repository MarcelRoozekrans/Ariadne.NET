using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;
using ZeroAlloc.Inject;

namespace Ariadne.NET.Analysis;

/// <summary>Builds the type graph of each project and target framework, runs the v1 rules and merges the results per project.</summary>
[Singleton]
public sealed class ArchitectureValidator : IArchitectureValidator
{
    /// <inheritdoc />
    public ArchitectureReport Validate(RulePack resolvedPack, Compilation compilation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        return Validate(resolvedPack, [new AnalyzedProject(compilation.AssemblyName ?? string.Empty, null, compilation)], cancellationToken);
    }

    /// <inheritdoc />
    public ArchitectureReport Validate(RulePack resolvedPack, IReadOnlyList<AnalyzedProject> projects, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(projects);

        RejectMixedTargetFrameworks(projects);

        var entries = new List<Entry>(projects.Count);
        foreach (var project in projects)
        {
            ArgumentNullException.ThrowIfNull(project, nameof(projects));
            cancellationToken.ThrowIfCancellationRequested();

            var graph = TypeGraphBuilder.Build(project.Compilation, project.GeneratedTrees, cancellationToken);
            var layers = new V1LayerMap(resolvedPack);
            var violations = new List<ArchitectureViolation>(V1LayerRules.Check(graph, layers, resolvedPack));
            var matches = V1RoleRules.Match(graph, layers, resolvedPack);
            violations.AddRange(V1RoleRules.CheckShape(matches));
            entries.Add(new Entry(project, graph, matches, violations));
        }

        AddMissingInterfaceViolations(entries, resolvedPack);

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

        var allViolations = new List<ArchitectureViolation>();
        var checkedProjects = new List<CheckedProject>(ordered.Count);
        var typesChecked = 0;
        foreach (ref readonly var group in CollectionsMarshal.AsSpan(ordered))
        {
            group.AddMergedViolations(allViolations);
            checkedProjects.Add(new CheckedProject(group.Name, group.TargetFrameworks()));
            typesChecked += group.TypesChecked;
        }

        allViolations.Sort(CompareViolations);
        return new ArchitectureReport
        {
            Violations = allViolations,
            Projects = checkedProjects,
            TypesChecked = typesChecked,
        };
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
    private static void AddMissingInterfaceViolations(List<Entry> entries, RulePack pack)
    {
        var byFramework = new Dictionary<ValueTuple<string?>, List<Entry>>();
        foreach (ref readonly var entry in CollectionsMarshal.AsSpan(entries))
        {
            var key = new ValueTuple<string?>(entry.Project.TargetFramework);
            if (!byFramework.TryGetValue(key, out var list))
            {
                list = [];
                byFramework.Add(key, list);
            }

            list.Add(entry);
        }

        foreach (var list in byFramework.Values)
        {
            var scope = new List<RoleMatches>(list.Count);
            foreach (ref readonly var entry in CollectionsMarshal.AsSpan(list))
            {
                scope.Add(entry.Matches);
            }

            foreach (ref readonly var entry in CollectionsMarshal.AsSpan(list))
            {
                entry.Violations.AddRange(V1RoleRules.CheckMissingInterface(entry.Matches, scope, pack));
            }
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

        // The earlier location wins, with its message; the reference count is the highest.
        private static ArchitectureViolation Merge(ArchitectureViolation current, ArchitectureViolation candidate)
        {
            var count = Math.Max(current.ReferenceCount, candidate.ReferenceCount);
            var candidateIsEarlier = candidate.Location is { } c
                && (current.Location is not { } l || TypeGraphBuilder.Compare(c, l) < 0);
            return (candidateIsEarlier ? candidate : current) with { ReferenceCount = count };
        }
    }
}
