using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The <c>slices</c> rules of a pack, evaluated once over the type graphs of every project of a run, since a slice usually
/// spans projects. A slice is the name a rule's <c>matching</c> patterns capture from a source type's namespace.
/// </summary>
internal static class SliceRules
{
    /// <summary>
    /// Returns, for an <c>independent</c> rule, one <see cref="ViolationKind.SliceDependency"/> per source and target type in
    /// different slices, with the number of references and the location of the first. Only source types of the run have a
    /// slice, targets included; a nested type sits in its outermost type's slice. A type is identified by its assembly name and
    /// display string, so a type seen in several target frameworks of one project counts once. An exception exempts a source
    /// type only, and a target that <c>allow</c> selects is not a dependency. Edges are checked per target framework scope, and
    /// a violation found in several scopes is reported once, naming the frameworks that found it, or none when every scope did.
    /// </summary>
    /// <param name="layers">The layer map of the pack.</param>
    /// <param name="pack">The resolved pack.</param>
    /// <param name="graphs">The type graphs of the run, each with its project's target framework.</param>
    /// <returns>The violations, in pack rule order.</returns>
    /// <exception cref="ArgumentException">A slices rule has no slices or a malformed pattern, which only a pack built in code can hold.</exception>
    public static IReadOnlyList<ArchitectureViolation> Check(
        LayerMap layers, RulePack pack, IReadOnlyList<(TypeGraph Graph, string? TargetFramework)> graphs)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(graphs);

        var violations = new List<ArchitectureViolation>();
        List<Scope>? scopes = null;
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind != RuleKind.Slices)
            {
                continue;
            }

            scopes ??= Scope.Build(graphs);
            var slices = SlicesOf(rule);
            var sliced = new SlicedTypes(Slicer.From(rule, slices), ExceptionsOf(rule), layers.Names);
            sliced.Collect(graphs);
            var allow = slices.Allow is { } selector ? TargetMatcher.From(selector, layers) : null;
            var perGraph = new List<SliceEdges>(graphs.Count);
            foreach (var (graph, _) in graphs)
            {
                perGraph.Add(SliceEdges.Of(graph, sliced, allow));
            }

            if (slices.Independent)
            {
                CheckIndependence(rule, scopes, perGraph, violations);
            }
        }

        return violations;
    }

    /// <summary>Whether the rule's patterns place at least one source type of the graphs in a slice.</summary>
    /// <param name="rule">A slices rule.</param>
    /// <param name="graphs">The type graphs of the run.</param>
    /// <returns>True when some type has a slice.</returns>
    /// <exception cref="ArgumentException">The rule has no slices or a malformed pattern.</exception>
    public static bool PlacesAnyType(RuleDefinition rule, IReadOnlyList<TypeGraph> graphs)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(graphs);

        var slicer = Slicer.From(rule, SlicesOf(rule));
        foreach (var graph in graphs)
        {
            foreach (var type in graph.SourceTypes)
            {
                if (slicer.SliceOf(type) is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static SliceDefinition SlicesOf(RuleDefinition rule) =>
        rule.Slices ?? throw new ArgumentException($"Slices rule '{rule.Id}' has no slices.", nameof(rule));

    private static TypeSelector[] ExceptionsOf(RuleDefinition rule)
    {
        var exceptions = new TypeSelector[rule.Exceptions.Count];
        for (var i = 0; i < exceptions.Length; i++)
        {
            exceptions[i] = TypeSelector.From(rule.Exceptions[i]);
        }

        return exceptions;
    }

    private static void CheckIndependence(RuleDefinition rule, List<Scope> scopes, List<SliceEdges> perGraph, List<ArchitectureViolation> violations)
    {
        var merged = new Dictionary<(SlicedType Source, SlicedType Target), Merged>();
        var order = new List<(SlicedType Source, SlicedType Target)>();
        foreach (ref readonly var scope in CollectionsMarshal.AsSpan(scopes))
        {
            foreach (ref readonly var pair in CollectionsMarshal.AsSpan(scope.Edges(perGraph)))
            {
                var (key, found) = pair;
                if (!merged.TryGetValue(key, out var entry))
                {
                    merged.Add(key, entry = new Merged(found.Location));
                    order.Add(key);
                }

                entry.Add(found, scope.Framework);
            }
        }

        foreach (ref readonly var key in CollectionsMarshal.AsSpan(order))
        {
            var entry = merged[key];
            violations.Add(new ArchitectureViolation
            {
                RuleId = rule.Id,
                Kind = ViolationKind.SliceDependency,
                SourceType = key.Source.Display,
                Target = key.Target.Display,
                Location = entry.Location,
                Because = rule.Because,
                ReferenceCount = entry.Count,
                TargetFrameworks = entry.Frameworks.Count == scopes.Count ? [] : entry.SortedNamedFrameworks(),
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Type '{key.Source.Display}' in slice '{key.Source.Slice}' depends on '{key.Target.Display}' in slice '{key.Target.Slice}', and rule '{rule.Id}' keeps slices independent{LayerRules.CountSuffix(entry.Count)}"),
            });
        }
    }

    /// <summary>The compiled patterns of one rule, with the slice of each namespace cached.</summary>
    private sealed class Slicer
    {
        private readonly SlicePattern[] _patterns;
        private readonly Dictionary<INamespaceSymbol, string?> _byNamespace = new(SymbolEqualityComparer.Default);

        private Slicer(SlicePattern[] patterns) => _patterns = patterns;

        public static Slicer From(RuleDefinition rule, SliceDefinition slices)
        {
            var patterns = new SlicePattern[slices.Matching.Count];
            for (var i = 0; i < patterns.Length; i++)
            {
                var compiled = SlicePattern.Compile(slices.Matching[i]);
                patterns[i] = compiled.IsSuccess
                    ? compiled.Value
                    : throw new ArgumentException($"Slices rule '{rule.Id}' has an invalid pattern: {compiled.Error}.", nameof(rule));
            }

            return new Slicer(patterns);
        }

        // The containing namespace of a nested type is its outermost type's; the global namespace has no slice. The first
        // pattern in list order that captures wins.
        public string? SliceOf(INamedTypeSymbol type)
        {
            var ns = type.ContainingNamespace;
            if (ns is null || ns.IsGlobalNamespace)
            {
                return null;
            }

            if (!_byNamespace.TryGetValue(ns, out var slice))
            {
                var name = ns.ToDisplayString();
                foreach (var pattern in _patterns)
                {
                    if (pattern.TryCapture(name, out var captured))
                    {
                        slice = captured;
                        break;
                    }
                }

                _byNamespace.Add(ns, slice);
            }

            return slice;
        }
    }

    /// <summary>The source types of the run that have a slice, keyed by identity and by symbol.</summary>
    private sealed class SlicedTypes(Slicer slicer, TypeSelector[] exceptions, TypeNames names)
    {
        private readonly Dictionary<TypeId, SlicedType> _byId = [];
        private readonly Dictionary<INamedTypeSymbol, SlicedType?> _bySymbol = new(SymbolEqualityComparer.Default);

        public void Collect(IReadOnlyList<(TypeGraph Graph, string? TargetFramework)> graphs)
        {
            foreach (var (graph, _) in graphs)
            {
                foreach (var type in graph.SourceTypes)
                {
                    if (_bySymbol.ContainsKey(type))
                    {
                        continue;
                    }

                    SlicedType? sliced = null;
                    if (slicer.SliceOf(type) is { } slice)
                    {
                        var id = IdOf(type);
                        if (!_byId.TryGetValue(id, out sliced))
                        {
                            _byId.Add(id, sliced = new SlicedType(id.Display, slice, IsExcepted(type)));
                        }
                    }

                    _bySymbol.Add(type, sliced);
                }
            }
        }

        // A target outside the run's source types, such as a framework or package type, has no slice even when its namespace matches.
        public SlicedType? Find(INamedTypeSymbol type)
        {
            if (!_bySymbol.TryGetValue(type, out var sliced))
            {
                if (slicer.SliceOf(type) is not null)
                {
                    _byId.TryGetValue(IdOf(type), out sliced);
                }

                _bySymbol.Add(type, sliced);
            }

            return sliced;
        }

        private static TypeId IdOf(INamedTypeSymbol type) => new(type.ContainingAssembly?.Name ?? string.Empty, type.ToDisplayString());

        private bool IsExcepted(INamedTypeSymbol type)
        {
            foreach (var exception in exceptions)
            {
                if (exception.Matches(type, names))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A source type with a slice; one instance per type identity, so instances compare by reference.</summary>
    private sealed class SlicedType(string display, string slice, bool excepted)
    {
        public string Display { get; } = display;

        public string Slice { get; } = slice;

        public bool Excepted { get; } = excepted;
    }

    /// <summary>The counted edges of one graph, aggregated per source and target type, in the order first seen.</summary>
    private sealed class SliceEdges
    {
        private readonly Dictionary<(SlicedType Source, SlicedType Target), int> _index = [];

        public List<((SlicedType Source, SlicedType Target) Key, Found Found)> Pairs { get; } = [];

        // An edge counts when its source is not excepted, both ends have a slice, the slices differ, and allow does not select the target.
        public static SliceEdges Of(TypeGraph graph, SlicedTypes sliced, TargetMatcher? allow)
        {
            var result = new SliceEdges();
            foreach (var edge in graph.Edges)
            {
                if (sliced.Find(edge.Source) is not { Excepted: false } source
                    || sliced.Find(edge.Target) is not { } target
                    || string.Equals(source.Slice, target.Slice, StringComparison.Ordinal)
                    || allow?.Matches(edge.Target) == true)
                {
                    continue;
                }

                result.Record((source, target), edge.Location);
            }

            return result;
        }

        // Edges are ordered by location, so the first one recorded is the earliest.
        private void Record((SlicedType Source, SlicedType Target) key, in SourceLocation location)
        {
            if (_index.TryGetValue(key, out var at))
            {
                Pairs[at].Found.Count++;
            }
            else
            {
                _index.Add(key, Pairs.Count);
                Pairs.Add((key, new Found(location)));
            }
        }
    }

    /// <summary>The references from one source type to one target type: how many, and the earliest.</summary>
    private sealed class Found(SourceLocation location)
    {
        public SourceLocation Location { get; set; } = location;

        public int Count { get; set; } = 1;

        public void Add(Found other)
        {
            Count += other.Count;
            if (TypeGraphBuilder.Compare(other.Location, Location) < 0)
            {
                Location = other.Location;
            }
        }
    }

    /// <summary>A pair found in several scopes: the earliest location, the highest count and the scopes' frameworks.</summary>
    private sealed class Merged(SourceLocation location)
    {
        public SourceLocation Location { get; private set; } = location;

        public int Count { get; private set; }

        public List<string?> Frameworks { get; } = [];

        public void Add(Found found, string? framework)
        {
            Count = Math.Max(Count, found.Count);
            if (TypeGraphBuilder.Compare(found.Location, Location) < 0)
            {
                Location = found.Location;
            }

            Frameworks.Add(framework);
        }

        public List<string> SortedNamedFrameworks()
        {
            var named = new List<string>(Frameworks.Count);
            foreach (ref readonly var framework in CollectionsMarshal.AsSpan(Frameworks))
            {
                if (framework is not null)
                {
                    named.Add(framework);
                }
            }

            named.Sort(StringComparer.Ordinal);
            return named;
        }
    }

    /// <summary>
    /// The graphs one check sees together, built as the validator builds the scopes of missing-interface checks: one per named
    /// target framework, holding that framework's graphs and every graph of a project without one, or one scope over all graphs
    /// when no project names a framework.
    /// </summary>
    private sealed class Scope(string? framework)
    {
        private readonly List<int> _graphs = [];

        public string? Framework { get; } = framework;

        public static List<Scope> Build(IReadOnlyList<(TypeGraph Graph, string? TargetFramework)> graphs)
        {
            var byFramework = new Dictionary<string, Scope>(StringComparer.Ordinal);
            var scopes = new List<Scope>();
            var unnamed = new List<int>();
            for (var i = 0; i < graphs.Count; i++)
            {
                if (graphs[i].TargetFramework is not { } framework)
                {
                    unnamed.Add(i);
                    continue;
                }

                if (!byFramework.TryGetValue(framework, out var scope))
                {
                    byFramework.Add(framework, scope = new Scope(framework));
                    scopes.Add(scope);
                }

                scope._graphs.Add(i);
            }

            if (scopes.Count == 0)
            {
                var all = new Scope(null);
                all._graphs.AddRange(unnamed);
                return [all];
            }

            foreach (ref readonly var scope in CollectionsMarshal.AsSpan(scopes))
            {
                scope._graphs.AddRange(unnamed);
            }

            return scopes;
        }

        /// <summary>The counted edges of the scope's graphs, summed per source and target type, in the order first seen.</summary>
        public List<((SlicedType Source, SlicedType Target) Key, Found Found)> Edges(List<SliceEdges> perGraph)
        {
            var index = new Dictionary<(SlicedType Source, SlicedType Target), Found>();
            var result = new List<((SlicedType Source, SlicedType Target) Key, Found Found)>();
            foreach (ref readonly var graph in CollectionsMarshal.AsSpan(_graphs))
            {
                foreach (ref readonly var pair in CollectionsMarshal.AsSpan(perGraph[graph].Pairs))
                {
                    var (key, found) = pair;
                    if (index.TryGetValue(key, out var sum))
                    {
                        sum.Add(found);
                    }
                    else
                    {
                        var copy = new Found(found.Location) { Count = found.Count };
                        index.Add(key, copy);
                        result.Add((key, copy));
                    }
                }
            }

            return result;
        }
    }

    // The assembly name is the project identity: the graphs of one project's target frameworks share it, while distinct
    // projects that declare the same full name stay apart.
    private readonly record struct TypeId(string Assembly, string Display);
}
