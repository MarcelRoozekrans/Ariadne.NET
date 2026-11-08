using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>A use of a member, such as a call or a property read, from a type's code at a location in source.</summary>
/// <param name="Source">The type whose code holds the use.</param>
/// <param name="Member">
/// The used member, as its original definition; an extension method call is the static method it reduces from. Its names
/// for matching come from <see cref="MemberNames.Chain(ISymbol)"/>.
/// </param>
/// <param name="Location">Where the member's name starts, or the <c>new</c> keyword of a constructor call.</param>
internal readonly record struct MemberEdge(INamedTypeSymbol Source, ISymbol Member, SourceLocation Location)
{
    /// <summary>Compares the symbols with <see cref="SymbolEqualityComparer.Default"/> and the locations by value.</summary>
    public bool Equals(MemberEdge other) =>
        SymbolEqualityComparer.Default.Equals(Source, other.Source)
        && SymbolEqualityComparer.Default.Equals(Member, other.Member)
        && Location.Equals(other.Location);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            SymbolEqualityComparer.Default.GetHashCode(Source),
            SymbolEqualityComparer.Default.GetHashCode(Member),
            Location);
}
