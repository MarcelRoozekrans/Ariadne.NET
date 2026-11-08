using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The type references and member uses found in a compilation's hand-written code.</summary>
internal sealed class TypeGraph
{
    public TypeGraph(IReadOnlyList<TypeEdge> edges, IReadOnlyList<MemberEdge> memberEdges, IReadOnlyList<INamedTypeSymbol> sourceTypes,
        IReadOnlyDictionary<INamedTypeSymbol, SourceLocation> sourceTypeLocations)
    {
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(memberEdges);
        ArgumentNullException.ThrowIfNull(sourceTypes);
        ArgumentNullException.ThrowIfNull(sourceTypeLocations);

        Edges = edges;
        MemberEdges = memberEdges;
        SourceTypes = sourceTypes;
        SourceTypeLocations = sourceTypeLocations;
    }

    /// <summary>The edges, de-duplicated on source, target and location, ordered by location.</summary>
    public IReadOnlyList<TypeEdge> Edges { get; }

    /// <summary>
    /// The member uses whose name passed the build's <see cref="MemberNameFilter"/>, de-duplicated on source, member and
    /// location, ordered by location. Empty when the filter is empty.
    /// </summary>
    public IReadOnlyList<MemberEdge> MemberEdges { get; }

    /// <summary>The types checked as edge sources: declared in a non-generated tree and without <c>[GeneratedCode]</c>, ordered by their first such declaration.</summary>
    public IReadOnlyList<INamedTypeSymbol> SourceTypes { get; }

    /// <summary>The 1-based position of the identifier of each source type's first non-generated declaration.</summary>
    public IReadOnlyDictionary<INamedTypeSymbol, SourceLocation> SourceTypeLocations { get; }
}
