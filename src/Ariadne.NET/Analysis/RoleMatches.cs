using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The source types of one type graph that match a component role of the pack.</summary>
internal sealed class RoleMatches
{
    public RoleMatches(TypeGraph graph, IReadOnlyList<(INamedTypeSymbol Type, ComponentRoleDefinition Role)> items)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(items);

        Graph = graph;
        Items = items;
    }

    public TypeGraph Graph { get; }

    public IReadOnlyList<(INamedTypeSymbol Type, ComponentRoleDefinition Role)> Items { get; }
}
