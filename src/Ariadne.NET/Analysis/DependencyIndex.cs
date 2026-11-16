using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The dependencies of every type declared in the run's projects: the targets of the <see cref="TypeGraph"/> edges whose source
/// is the type, as original definitions, in source order and without duplicates. Each compilation keeps its own symbols, so the
/// target framework variants of one project never mix. A metadata symbol of a run project's type, as another project sees it, is
/// found in the graph that project compiles against.
/// </summary>
internal sealed class DependencyIndex
{
    private readonly Dictionary<INamedTypeSymbol, IReadOnlyList<INamedTypeSymbol>> _dependencies;
    private readonly Dictionary<IAssemblySymbol, int> _graphOfAssembly;
    private readonly List<Dictionary<string, INamedTypeSymbol>> _typesByMetadataName;

    private DependencyIndex(
        Dictionary<INamedTypeSymbol, IReadOnlyList<INamedTypeSymbol>> dependencies,
        Dictionary<IAssemblySymbol, int> graphOfAssembly,
        List<Dictionary<string, INamedTypeSymbol>> typesByMetadataName)
    {
        _dependencies = dependencies;
        _graphOfAssembly = graphOfAssembly;
        _typesByMetadataName = typesByMetadataName;
    }

    /// <summary>Indexes the dependencies of every source type of the run's graphs.</summary>
    /// <param name="projects">The projects of the run.</param>
    /// <param name="graphs">The type graph of each project, in the same order.</param>
    /// <returns>The index.</returns>
    public static DependencyIndex Build(IReadOnlyList<AnalyzedProject> projects, IReadOnlyList<TypeGraph> graphs)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(graphs);

        var dependencies = new Dictionary<INamedTypeSymbol, IReadOnlyList<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        var typesByMetadataName = new List<Dictionary<string, INamedTypeSymbol>>(graphs.Count);
        foreach (var graph in graphs)
        {
            typesByMetadataName.Add(IndexGraph(graph, dependencies));
        }

        return new DependencyIndex(dependencies, MapAssemblies(projects, ScopedGraph.Of(projects, graphs)), typesByMetadataName);
    }

    /// <summary>The dependencies of the type, in source order; empty for a type declared outside the run's projects.</summary>
    /// <param name="type">The type, constructed or not.</param>
    /// <returns>The dependency targets as original definitions.</returns>
    public IReadOnlyList<INamedTypeSymbol> DependenciesOf(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var definition = type.OriginalDefinition;
        if (_dependencies.TryGetValue(definition, out var direct))
        {
            return direct;
        }

        return Resolve(definition) is var (resolved, graph)
            && _typesByMetadataName[graph].TryGetValue(MetadataNameOf(resolved), out var source)
            && _dependencies.TryGetValue(source, out var found)
            ? found
            : [];
    }

    /// <summary>Whether the type is declared in one of the run's projects, in hand-written or generated code.</summary>
    /// <param name="type">The type, constructed or not.</param>
    /// <returns>True when a graph of the run declares the type.</returns>
    public bool IsDeclaredInRun(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var definition = type.OriginalDefinition;
        return _dependencies.ContainsKey(definition) || Resolve(definition) is not null;
    }

    /// <summary>The type after following a type forward, and the graph that declares its assembly; null when no run project does.</summary>
    private (INamedTypeSymbol Type, int Graph)? Resolve(INamedTypeSymbol definition)
    {
        if (definition.ContainingAssembly is not { } assembly)
        {
            return null;
        }

        if (assembly.ResolveForwardedType(MetadataNameOf(definition)) is { } forwarded)
        {
            definition = forwarded.OriginalDefinition;
            if (definition.ContainingAssembly is not { } target)
            {
                return null;
            }

            assembly = target;
        }

        return _graphOfAssembly.TryGetValue(assembly, out var graph) ? (definition, graph) : null;
    }

    private static Dictionary<string, INamedTypeSymbol> IndexGraph(
        TypeGraph graph, Dictionary<INamedTypeSymbol, IReadOnlyList<INamedTypeSymbol>> dependencies)
    {
        var byMetadataName = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var targets = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        var seen = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var type in graph.SourceTypes)
        {
            byMetadataName.TryAdd(MetadataNameOf(type), type);
            targets.TryAdd(type, []);
        }

        foreach (var edge in graph.Edges)
        {
            if (!targets.TryGetValue(edge.Source, out var list))
            {
                list = [];
                targets.Add(edge.Source, list);
                byMetadataName.TryAdd(MetadataNameOf(edge.Source), edge.Source);
            }

            var target = edge.Target.OriginalDefinition;
            if (!seen.TryGetValue(edge.Source, out var sourceSeen))
            {
                sourceSeen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                seen.Add(edge.Source, sourceSeen);
            }

            if (!SymbolEqualityComparer.Default.Equals(target, edge.Source) && sourceSeen.Add(target))
            {
                list.Add(target);
            }
        }

        foreach (var (type, list) in targets)
        {
            dependencies.TryAdd(type, list.Count == 0 ? [] : list);
        }

        return byMetadataName;
    }

    /// <summary>
    /// Maps every assembly symbol that names a run project to that project's graph: each compilation's own assembly, and each
    /// assembly a compilation references. A <see cref="CompilationReference"/> to a run compilation names its graph exactly. Any
    /// other reference is matched by assembly identity, preferring a graph the referencing project's compilation references,
    /// then one of the same target framework, then the first.
    /// </summary>
    private static Dictionary<IAssemblySymbol, int> MapAssemblies(IReadOnlyList<AnalyzedProject> projects, List<ScopedGraph> scoped)
    {
        var result = new Dictionary<IAssemblySymbol, int>(SymbolEqualityComparer.Default);
        var indexOf = new Dictionary<Compilation, int>(projects.Count, ReferenceEqualityComparer.Instance);
        var byIdentity = new Dictionary<AssemblyIdentity, List<int>>();
        for (var i = 0; i < projects.Count; i++)
        {
            var compilation = projects[i].Compilation;
            indexOf.TryAdd(compilation, i);
            result.TryAdd(compilation.Assembly, i);
            if (!byIdentity.TryGetValue(compilation.Assembly.Identity, out var list))
            {
                list = [];
                byIdentity.Add(compilation.Assembly.Identity, list);
            }

            list.Add(i);
        }

        for (var j = 0; j < projects.Count; j++)
        {
            var compilation = projects[j].Compilation;
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly || result.ContainsKey(assembly))
                {
                    continue;
                }

                if (reference is CompilationReference { Compilation: var referenced } && indexOf.TryGetValue(referenced, out var exact))
                {
                    result.Add(assembly, exact);
                }
                else if (byIdentity.TryGetValue(assembly.Identity, out var candidates))
                {
                    result.Add(assembly, Prefer(candidates, scoped[j].References, projects[j].TargetFramework, projects));
                }
            }
        }

        return result;
    }

    private static int Prefer(
        IReadOnlyList<int> candidates, IReadOnlyList<int> referenced, string? targetFramework, IReadOnlyList<AnalyzedProject> projects)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (referenced.Contains(candidates[i]))
            {
                return candidates[i];
            }
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(projects[candidates[i]].TargetFramework, targetFramework, StringComparison.OrdinalIgnoreCase))
            {
                return candidates[i];
            }
        }

        return candidates[0];
    }

    /// <summary>The full metadata name: the namespace, then the containing types and the type joined by <c>+</c>, with arity suffixes.</summary>
    private static string MetadataNameOf(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        var outer = type;
        while (outer.ContainingType is { } containing)
        {
            name = containing.MetadataName + "+" + name;
            outer = containing;
        }

        for (var ns = outer.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
        {
            name = ns.MetadataName + "." + name;
        }

        return name;
    }
}
