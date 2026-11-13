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
    /// type only, evaluated for each target framework variant, and a target that <c>allow</c> selects is not a dependency. Edges are checked per target framework scope,
    /// which also holds, for a project without that framework, the variant the framework's build compiles against, and
    /// a violation found in several scopes is reported once, naming the frameworks that found it, or none when every scope did.
    /// For a <c>freeOfCycles</c> rule it returns one <see cref="ViolationKind.SliceCycle"/> per slice edge and component: one for each
    /// edge whose two slices share a strongly connected component of the scope's slice graph, through the earliest reference from the one to the other.
    /// </summary>
    /// <param name="layers">The layer map of the pack.</param>
    /// <param name="pack">The resolved pack.</param>
    /// <param name="graphs">The type graphs of the run, each with its project, target framework and referenced graphs.</param>
    /// <returns>The violations, in pack rule order.</returns>
    /// <exception cref="ArgumentException">A slices rule has no slices or a malformed pattern, which only a pack built in code can hold.</exception>
    public static IReadOnlyList<ArchitectureViolation> Check(
        LayerMap layers, RulePack pack, IReadOnlyList<ScopedGraph> graphs)
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
            foreach (var scoped in graphs)
            {
                perGraph.Add(SliceEdges.Of(scoped.Graph, sliced, allow));
            }

            if (slices.Independent)
            {
                CheckIndependence(rule, scopes, perGraph, violations);
            }
            else
            {
                CheckCycles(rule, scopes, perGraph, violations);
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

    private static List<string> SortedNamed(List<string?> frameworks)
    {
        var named = new List<string>(frameworks.Count);
        foreach (ref readonly var framework in CollectionsMarshal.AsSpan(frameworks))
        {
            if (framework is not null)
            {
                named.Add(framework);
            }
        }

        named.Sort(StringComparer.Ordinal);
        return named;
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

    private static void CheckCycles(RuleDefinition rule, List<Scope> scopes, List<SliceEdges> perGraph, List<ArchitectureViolation> violations)
    {
        var merged = new Dictionary<(string From, string To, string Cycle), CycleEdge>();
        var order = new List<(string From, string To, string Cycle)>();
        foreach (ref readonly var scope in CollectionsMarshal.AsSpan(scopes))
        {
            var graph = SliceGraph.Of(scope.Edges(perGraph));
            var cycles = graph.Cycles();
            foreach (ref readonly var edge in CollectionsMarshal.AsSpan(graph.Edges))
            {
                if (cycles[edge.From] is not { } cycle || !ReferenceEquals(cycle, cycles[edge.To]))
                {
                    continue;
                }

                var key = (graph.Names[edge.From], graph.Names[edge.To], cycle);
                if (!merged.TryGetValue(key, out var entry))
                {
                    merged.Add(key, entry = new CycleEdge(edge.Source, edge.Target, edge.Location));
                    order.Add(key);
                }

                entry.Add(edge, scope.Framework);
            }
        }

        foreach (ref readonly var key in CollectionsMarshal.AsSpan(order))
        {
            var entry = merged[key];
            violations.Add(new ArchitectureViolation
            {
                RuleId = rule.Id,
                Kind = ViolationKind.SliceCycle,
                SourceType = entry.Source.Display,
                Target = entry.Target.Display,
                Location = entry.Location,
                Because = rule.Because,
                ReferenceCount = entry.Count,
                TargetFrameworks = entry.Frameworks.Count == scopes.Count ? [] : entry.SortedNamedFrameworks(),
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Slice '{key.From}' depends on slice '{key.To}' (first through '{entry.Source.Display}' -> '{entry.Target.Display}'), and slices {key.Cycle} form a cycle, which rule '{rule.Id}' forbids"),
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
        private readonly Dictionary<INamedTypeSymbol, bool> _exceptedBySymbol = new(SymbolEqualityComparer.Default);

        public void Collect(IReadOnlyList<ScopedGraph> graphs)
        {
            foreach (var scoped in graphs)
            {
                foreach (var type in scoped.Graph.SourceTypes)
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
                            _byId.Add(id, sliced = new SlicedType(id.Display, slice));
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

        // Exemption belongs to the symbol, not the shared identity: a target framework variant can carry an attribute or a
        // modifier that another variant lacks, and the exception must not carry over between them.
        public bool IsExceptedSource(INamedTypeSymbol type)
        {
            if (!_exceptedBySymbol.TryGetValue(type, out var excepted))
            {
                _exceptedBySymbol.Add(type, excepted = IsExcepted(type));
            }

            return excepted;
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
    private sealed class SlicedType(string display, string slice)
    {
        public string Display { get; } = display;

        public string Slice { get; } = slice;
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
                if (sliced.Find(edge.Source) is not { } source
                    || sliced.IsExceptedSource(edge.Source)
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

        public List<string> SortedNamedFrameworks() => SortedNamed(Frameworks);
    }

    /// <summary>One edge between two slices of a scope: the number of references and the earliest of them.</summary>
    private sealed class SliceEdge(int from, int to, SlicedType source, SlicedType target, Found found)
    {
        public int From { get; } = from;

        public int To { get; } = to;

        public SlicedType Source { get; private set; } = source;

        public SlicedType Target { get; private set; } = target;

        public SourceLocation Location { get; private set; } = found.Location;

        public int Count { get; private set; } = found.Count;

        public void Add(SlicedType source, SlicedType target, Found found)
        {
            Count += found.Count;
            if (TypeGraphBuilder.Compare(found.Location, Location) < 0)
            {
                Source = source;
                Target = target;
                Location = found.Location;
            }
        }
    }

    /// <summary>The slices of one scope and the edges between them, in the order first seen.</summary>
    private sealed class SliceGraph
    {
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
        private readonly Dictionary<(int From, int To), SliceEdge> _index = [];

        public List<string> Names { get; } = [];

        public List<SliceEdge> Edges { get; } = [];

        public static SliceGraph Of(List<((SlicedType Source, SlicedType Target) Key, Found Found)> pairs)
        {
            var graph = new SliceGraph();
            foreach (ref readonly var pair in CollectionsMarshal.AsSpan(pairs))
            {
                var ((source, target), found) = pair;
                var from = graph.IdOf(source.Slice);
                var to = graph.IdOf(target.Slice);
                if (graph._index.TryGetValue((from, to), out var edge))
                {
                    edge.Add(source, target, found);
                }
                else
                {
                    edge = new SliceEdge(from, to, source, target, found);
                    graph._index.Add((from, to), edge);
                    graph.Edges.Add(edge);
                }
            }

            return graph;
        }

        /// <summary>
        /// Returns per slice the ordinally sorted, comma-separated names of its strongly connected component when that holds
        /// two or more slices, else null. Slices of one component share one string instance.
        /// </summary>
        public string?[] Cycles()
        {
            var cycles = new string?[Names.Count];
            if (Edges.Count > 1)
            {
                new Tarjan(this, cycles).Run();
            }

            return cycles;
        }

        /// <summary>Tarjan's strongly connected components, iterative, over the slice graph's edges in compressed rows.</summary>
        private sealed class Tarjan
        {
            private readonly SliceGraph _graph;
            private readonly string?[] _cycles;
            private readonly int[] _offsets;
            private readonly int[] _successors;
            private readonly int[] _index;
            private readonly int[] _low;
            private readonly bool[] _onStack;
            private readonly Stack<int> _stack = new();
            private readonly Stack<(int Node, int Next)> _calls = new();
            private int _next;

            public Tarjan(SliceGraph graph, string?[] cycles)
            {
                _graph = graph;
                _cycles = cycles;
                var count = graph.Names.Count;
                _offsets = new int[count + 1];
                _successors = new int[graph.Edges.Count];
                _index = new int[count];
                _low = new int[count];
                _onStack = new bool[count];
                Array.Fill(_index, -1);
                foreach (ref readonly var edge in CollectionsMarshal.AsSpan(graph.Edges))
                {
                    _offsets[edge.From + 1]++;
                }

                for (var i = 1; i <= count; i++)
                {
                    _offsets[i] += _offsets[i - 1];
                }

                var cursor = _offsets[..count];
                foreach (ref readonly var edge in CollectionsMarshal.AsSpan(graph.Edges))
                {
                    _successors[cursor[edge.From]++] = edge.To;
                }
            }

            public void Run()
            {
                for (var root = 0; root < _index.Length; root++)
                {
                    if (_index[root] < 0)
                    {
                        Visit(root);
                    }
                }
            }

            private void Visit(int root)
            {
                Enter(root);
                while (_calls.Count > 0)
                {
                    var (node, next) = _calls.Pop();
                    if (next < _offsets[node + 1])
                    {
                        _calls.Push((node, next + 1));
                        var successor = _successors[next];
                        if (_index[successor] < 0)
                        {
                            Enter(successor);
                        }
                        else if (_onStack[successor])
                        {
                            _low[node] = Math.Min(_low[node], _index[successor]);
                        }

                        continue;
                    }

                    if (_calls.TryPeek(out var caller))
                    {
                        _low[caller.Node] = Math.Min(_low[caller.Node], _low[node]);
                    }

                    if (_low[node] == _index[node])
                    {
                        PopComponent(node);
                    }
                }
            }

            private void Enter(int node)
            {
                _index[node] = _low[node] = _next++;
                _stack.Push(node);
                _onStack[node] = true;
                _calls.Push((node, _offsets[node]));
            }

            private void PopComponent(int root)
            {
                var members = new List<int>();
                int member;
                do
                {
                    member = _stack.Pop();
                    _onStack[member] = false;
                    members.Add(member);
                }
                while (member != root);

                if (members.Count < 2)
                {
                    return;
                }

                var names = new string[members.Count];
                for (var i = 0; i < names.Length; i++)
                {
                    names[i] = _graph.Names[members[i]];
                }

                Array.Sort(names, StringComparer.Ordinal);
                var cycle = string.Join(", ", names);
                foreach (ref readonly var m in CollectionsMarshal.AsSpan(members))
                {
                    _cycles[m] = cycle;
                }
            }
        }

        private int IdOf(string slice)
        {
            if (!_ids.TryGetValue(slice, out var id))
            {
                _ids.Add(slice, id = Names.Count);
                Names.Add(slice);
            }

            return id;
        }
    }

    /// <summary>A slice edge of a cycle found in several scopes: the earliest reference, the highest count and the frameworks.</summary>
    private sealed class CycleEdge(SlicedType source, SlicedType target, SourceLocation location)
    {
        public SlicedType Source { get; private set; } = source;

        public SlicedType Target { get; private set; } = target;

        public SourceLocation Location { get; private set; } = location;

        public int Count { get; private set; }

        public List<string?> Frameworks { get; } = [];

        public void Add(SliceEdge edge, string? framework)
        {
            Count = Math.Max(Count, edge.Count);
            if (TypeGraphBuilder.Compare(edge.Location, Location) < 0)
            {
                Source = edge.Source;
                Target = edge.Target;
                Location = edge.Location;
            }

            Frameworks.Add(framework);
        }

        public List<string> SortedNamedFrameworks() => SortedNamed(Frameworks);
    }

    /// <summary>
    /// The graphs one check sees together: one scope per named target framework, holding that framework's graphs, every graph of
    /// a project without one, and, for any other project, the variant those graphs reference, directly or through another
    /// referenced variant; or one scope over all graphs when no project names a framework.
    /// </summary>
    private sealed class Scope(string? framework)
    {
        private readonly List<int> _graphs = [];

        public string? Framework { get; } = framework;

        public static List<Scope> Build(IReadOnlyList<ScopedGraph> graphs)
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
                scope.AddReferencedVariants(graphs);
            }

            return scopes;
        }

        // Walks the references of the scope's graphs breadth-first and adds each referenced graph whose project has no variant in
        // the scope yet: the variant this framework's build compiles against. A project's own variant for the framework was added
        // first, so it always wins, and the scope keeps one variant per project, which keeps Edges from counting a reference twice.
        // When graphs of the scope reference different variants of one project, the first reached in graph order joins.
        private void AddReferencedVariants(IReadOnlyList<ScopedGraph> graphs)
        {
            var members = new HashSet<int>(_graphs);
            var projects = new HashSet<string>(StringComparer.Ordinal);
            foreach (ref readonly var member in CollectionsMarshal.AsSpan(_graphs))
            {
                projects.Add(graphs[member].Project);
            }

            for (var next = 0; next < _graphs.Count; next++)
            {
                foreach (var referenced in graphs[_graphs[next]].References)
                {
                    if (!members.Contains(referenced) && projects.Add(graphs[referenced].Project))
                    {
                        members.Add(referenced);
                        _graphs.Add(referenced);
                    }
                }
            }
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
