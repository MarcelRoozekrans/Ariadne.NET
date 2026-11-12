using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Selects types by namespace, assembly, type-name, base-type, interface and attribute globs, and by the type-level
/// traits of <see cref="TypeTraits"/>. The lists and traits are AND-ed, the values within a list are OR-ed and an empty
/// list or unset trait does not constrain.
/// </summary>
internal sealed class TypeSelector
{
    private readonly Glob[] _namespaces;
    private readonly Glob[] _assemblies;
    private readonly Glob[] _types;
    private readonly Glob[] _baseTypes;
    private readonly Glob[] _interfaces;
    private readonly Glob[] _attributes;
    private readonly Glob[] _members;
    private readonly TypeTraits _traits;

    private TypeSelector(Glob[] namespaces, Glob[] assemblies, Glob[] types, Glob[] baseTypes, Glob[] interfaces, Glob[] attributes, Glob[] members, TypeTraits traits)
    {
        _traits = traits;
        _members = members;
        _namespaces = namespaces;
        _assemblies = assemblies;
        _types = types;
        _baseTypes = baseTypes;
        _interfaces = interfaces;
        _attributes = attributes;
    }

    /// <summary>Whether the selector holds <c>members</c> globs, which select member uses and never types.</summary>
    public bool HasMembers => _members.Length > 0;

    /// <summary>Whether no type list or trait constrains anything, so the selector would match every type. The <c>members</c> globs do not count: they never select a type.</summary>
    public bool IsEmpty => _namespaces.Length == 0 && _assemblies.Length == 0 && _types.Length == 0
        && _baseTypes.Length == 0 && _interfaces.Length == 0 && _attributes.Length == 0 && _traits.IsEmpty;

    /// <summary>Compiles every list and trait of a selector once. <c>layers</c> is not a type selector and is ignored here. The loader validates patterns and values, so an invalid one is a programming error.</summary>
    /// <param name="selector">The selector to compile.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern or an accessibility is invalid.</exception>
    public static TypeSelector From(TargetSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Compile(
            selector.Namespaces,
            selector.Assemblies,
            selector.Types,
            selector.BaseTypes,
            selector.Interfaces,
            selector.Attributes,
            selector.Members,
            TypeTraits.From(selector));
    }

    /// <summary>
    /// Compiles a rule exception once: its <c>namespaces</c>, <c>assemblies</c> and <c>types</c> globs and its traits, all
    /// AND-ed. The loader validates patterns and values, so an invalid one is a programming error.
    /// </summary>
    /// <param name="exception">The exception to compile.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern or an accessibility is invalid.</exception>
    public static TypeSelector From(RuleException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Compile(exception.Namespaces, exception.Assemblies, exception.Types, [], [], [], [], TypeTraits.From(exception));
    }

    /// <summary>Compiles the patterns once. The loader validates patterns, so an invalid one is a programming error.</summary>
    /// <param name="namespaces">Dotted namespace globs.</param>
    /// <param name="assemblies">Flat assembly-name globs.</param>
    /// <param name="types">Dotted globs over the type name, containing types included and arity left out.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern is invalid.</exception>
    public static TypeSelector From(IList<string> namespaces, IList<string> assemblies, IList<string> types) =>
        From(namespaces, assemblies, types, [], [], []);

    /// <summary>Compiles the patterns once. The loader validates patterns, so an invalid one is a programming error.</summary>
    /// <param name="namespaces">Dotted namespace globs.</param>
    /// <param name="assemblies">Flat assembly-name globs.</param>
    /// <param name="types">Dotted globs over the type name, containing types included and arity left out.</param>
    /// <param name="baseTypes">Dotted globs over every class in the base chain, System.Object left out.</param>
    /// <param name="interfaces">Dotted globs over every interface the type implements, inherited ones included.</param>
    /// <param name="attributes">Dotted globs over the full name of the attribute classes applied to the type.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern is invalid.</exception>
    public static TypeSelector From(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string> types,
        IList<string> baseTypes,
        IList<string> interfaces,
        IList<string> attributes) =>
        From(namespaces, assemblies, types, baseTypes, interfaces, attributes, []);

    /// <summary>Compiles the patterns once. The loader validates patterns, so an invalid one is a programming error.</summary>
    /// <param name="namespaces">Dotted namespace globs.</param>
    /// <param name="assemblies">Flat assembly-name globs.</param>
    /// <param name="types">Dotted globs over the type name, containing types included and arity left out.</param>
    /// <param name="baseTypes">Dotted globs over every class in the base chain, System.Object left out.</param>
    /// <param name="interfaces">Dotted globs over every interface the type implements, inherited ones included.</param>
    /// <param name="attributes">Dotted globs over the full name of the attribute classes applied to the type.</param>
    /// <param name="members">Dotted globs over <c>Namespace.Type.Member</c>, matched by <see cref="AnyMemberMatches"/>.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern is invalid.</exception>
    public static TypeSelector From(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string> types,
        IList<string> baseTypes,
        IList<string> interfaces,
        IList<string> attributes,
        IList<string> members) =>
        Compile(namespaces, assemblies, types, baseTypes, interfaces, attributes, members, TypeTraits.None);

    private static TypeSelector Compile(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string> types,
        IList<string> baseTypes,
        IList<string> interfaces,
        IList<string> attributes,
        IList<string> members,
        TypeTraits traits)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(namespaces);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(baseTypes);
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(attributes);

        return new TypeSelector(
            Glob.CompileAll(namespaces, Glob.Dotted),
            Glob.CompileAll(assemblies, Glob.Flat),
            Glob.CompileAll(types, Glob.Dotted),
            Glob.CompileAll(baseTypes, Glob.Dotted),
            Glob.CompileAll(interfaces, Glob.Dotted),
            Glob.CompileAll(attributes, Glob.Dotted),
            Glob.CompileAll(members, Glob.Dotted),
            traits);
    }

    /// <summary>Whether a member name matches any <c>members</c> glob. Ordinal and case-sensitive.</summary>
    /// <param name="memberName">A <c>Namespace.Type.Member</c> name.</param>
    /// <returns>True when a glob matches.</returns>
    public bool AnyMemberMatches(string memberName) => Glob.AnyMatch(_members, memberName);

    /// <summary>Whether the type satisfies every non-empty list, then every set trait. Ordinal and case-sensitive.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="names">The names of the check, so each type's namespace and full name are built once.</param>
    /// <returns>True when the type is selected.</returns>
    public bool Matches(INamedTypeSymbol type, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(names);

        if (_namespaces.Length > 0 && !Glob.AnyMatch(_namespaces, names.NamespaceOf(type)))
        {
            return false;
        }

        if (_assemblies.Length > 0 && !Glob.AnyMatch(_assemblies, type.ContainingAssembly?.Name ?? string.Empty))
        {
            return false;
        }

        if (_types.Length > 0 && !Glob.AnyMatch(_types, names.FullNameOf(type)))
        {
            return false;
        }

        return (_baseTypes.Length == 0 || AnyBaseMatches(type, names))
            && (_interfaces.Length == 0 || AnyInterfaceMatches(type, names))
            && (_attributes.Length == 0 || AnyAttributeMatches(type, names))
            && (_traits.IsEmpty || _traits.Matches(type, names));
    }

    private bool AnyBaseMatches(INamedTypeSymbol type, TypeNames names)
    {
        for (var current = type.BaseType; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
        {
            if (Glob.AnyMatch(_baseTypes, names.FullNameOf(current.OriginalDefinition)))
            {
                return true;
            }
        }

        return false;
    }

    private bool AnyInterfaceMatches(INamedTypeSymbol type, TypeNames names)
    {
        foreach (var implemented in type.AllInterfaces)
        {
            if (Glob.AnyMatch(_interfaces, names.FullNameOf(implemented.OriginalDefinition)))
            {
                return true;
            }
        }

        return false;
    }

    private bool AnyAttributeMatches(INamedTypeSymbol type, TypeNames names)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass && Glob.AnyMatch(_attributes, names.FullNameOf(attributeClass.OriginalDefinition)))
            {
                return true;
            }
        }

        return false;
    }
}
