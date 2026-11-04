using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>A reference from one type to another at a location in source.</summary>
/// <param name="Source">The type whose code holds the reference.</param>
/// <param name="Target">The referenced type, as its original definition.</param>
/// <param name="Location">Where the referencing name starts.</param>
internal readonly record struct TypeEdge(INamedTypeSymbol Source, INamedTypeSymbol Target, SourceLocation Location)
{
    /// <summary>Compares the types with <see cref="SymbolEqualityComparer.Default"/> and the locations by value.</summary>
    public bool Equals(TypeEdge other) =>
        SymbolEqualityComparer.Default.Equals(Source, other.Source)
        && SymbolEqualityComparer.Default.Equals(Target, other.Target)
        && Location.Equals(other.Location);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            SymbolEqualityComparer.Default.GetHashCode(Source),
            SymbolEqualityComparer.Default.GetHashCode(Target),
            Location);
}
