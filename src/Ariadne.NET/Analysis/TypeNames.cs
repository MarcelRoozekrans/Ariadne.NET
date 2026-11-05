using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The namespace and full name of types, built once per symbol and shared by every selector of a check. One instance
/// serves one single-threaded check.
/// </summary>
internal sealed class TypeNames
{
    private readonly Dictionary<INamespaceSymbol, string> _namespaces = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, string> _fullNames = new(SymbolEqualityComparer.Default);

    /// <summary>The dotted name of the type's namespace, empty for the global namespace.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The namespace name.</returns>
    public string NamespaceOf(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.ContainingNamespace is not { IsGlobalNamespace: false } ns)
        {
            return string.Empty;
        }

        if (!_namespaces.TryGetValue(ns, out var name))
        {
            name = ns.ToDisplayString();
            _namespaces.Add(ns, name);
        }

        return name;
    }

    /// <summary>Namespace, containing types and name joined with dots. The name carries no arity, so <c>Foo&lt;T&gt;</c> is <c>Foo</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The full name.</returns>
    public string FullNameOf(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_fullNames.TryGetValue(type, out var name))
        {
            return name;
        }

        var prefix = type.ContainingType is { } outer ? FullNameOf(outer) : NamespaceOf(type);
        name = prefix.Length == 0 ? type.Name : string.Concat(prefix, ".", type.Name);
        _fullNames.Add(type, name);
        return name;
    }
}
