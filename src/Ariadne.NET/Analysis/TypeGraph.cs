using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The type references found in a compilation's hand-written code.</summary>
internal sealed class TypeGraph
{
    public TypeGraph(IReadOnlyList<TypeEdge> edges, IReadOnlyList<INamedTypeSymbol> sourceTypes,
        IReadOnlyDictionary<INamedTypeSymbol, SourceLocation> sourceTypeLocations)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(sourceTypes);
        ArgumentNullException.ThrowIfNull(sourceTypeLocations);

        Edges = edges;
        SourceTypes = sourceTypes;
        SourceTypeLocations = sourceTypeLocations;
    }

    /// <summary>The edges, de-duplicated on source, target and location, ordered by location.</summary>
    public IReadOnlyList<TypeEdge> Edges { get; }

    /// <summary>The types checked as edge sources: declared in a non-generated tree and without <c>[GeneratedCode]</c>, ordered by their first such declaration.</summary>
    public IReadOnlyList<INamedTypeSymbol> SourceTypes { get; }

    /// <summary>The 1-based position of the identifier of each source type's first non-generated declaration.</summary>
    public IReadOnlyDictionary<INamedTypeSymbol, SourceLocation> SourceTypeLocations { get; }
}
