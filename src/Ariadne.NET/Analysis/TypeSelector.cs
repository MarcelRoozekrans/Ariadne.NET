using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Selects types by namespace, assembly and type-name globs. The three lists are AND-ed, the values within a list are OR-ed
/// and an empty list does not constrain.
/// </summary>
internal sealed class TypeSelector
{
    private readonly Glob[] _namespaces;
    private readonly Glob[] _assemblies;
    private readonly Glob[] _types;

    private TypeSelector(Glob[] namespaces, Glob[] assemblies, Glob[] types)
    {
        _namespaces = namespaces;
        _assemblies = assemblies;
        _types = types;
    }

    /// <summary>Whether no list constrains anything, so the selector would match every type.</summary>
    public bool IsEmpty => _namespaces.Length == 0 && _assemblies.Length == 0 && _types.Length == 0;

    /// <summary>Compiles the patterns once. The loader validates patterns, so an invalid one is a programming error.</summary>
    /// <param name="namespaces">Dotted namespace globs.</param>
    /// <param name="assemblies">Flat assembly-name globs.</param>
    /// <param name="types">Dotted globs over the type name, containing types included and arity left out.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern is invalid.</exception>
    public static TypeSelector From(IList<string> namespaces, IList<string> assemblies, IList<string> types)
    {
        ArgumentNullException.ThrowIfNull(namespaces);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(types);

        return new TypeSelector(
            Compile(namespaces, Glob.Dotted),
            Compile(assemblies, Glob.Flat),
            Compile(types, Glob.Dotted));
    }

    /// <summary>Whether the type satisfies every non-empty list. Ordinal and case-sensitive.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the type is selected.</returns>
    public bool Matches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_namespaces.Length > 0 && !AnyMatch(_namespaces, type.ContainingNamespace?.ToDisplayString() ?? string.Empty))
        {
            return false;
        }

        if (_assemblies.Length > 0 && !AnyMatch(_assemblies, type.ContainingAssembly?.Name ?? string.Empty))
        {
            return false;
        }

        return _types.Length == 0 || AnyMatch(_types, FullName(type));
    }

    private static Glob[] Compile(IList<string> patterns, Func<string, ZeroAlloc.Results.Result<Glob, string>> compile)
    {
        var globs = new Glob[patterns.Count];
        for (var i = 0; i < globs.Length; i++)
        {
            var result = compile(patterns[i]);
            if (result.IsFailure)
            {
                throw new ArgumentException(result.Error, nameof(patterns));
            }

            globs[i] = result.Value;
        }

        return globs;
    }

    private static bool AnyMatch(Glob[] globs, string name)
    {
        foreach (var glob in globs)
        {
            if (glob.IsMatch(name))
            {
                return true;
            }
        }

        return false;
    }

    // Namespace, containing types and Name joined with dots. Name carries no arity, so Foo<T> is Foo.
    private static string FullName(INamedTypeSymbol type)
    {
        var prefix = type.ContainingType is { } outer
            ? FullName(outer)
            : type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : string.Empty;
        return prefix.Length == 0 ? type.Name : string.Concat(prefix, ".", type.Name);
    }
}
