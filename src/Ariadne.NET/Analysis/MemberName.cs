using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>A member in a use's chain: its <c>Namespace.Type.Member</c> name and the symbol, as an original definition.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Member">The member.</param>
internal readonly record struct MemberName(string Name, ISymbol Member);
