using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Selects types by namespace, assembly, type-name, base-type, interface and attribute globs, by attribute arguments, by an
/// attribute allow-list, and by the type-level traits of <see cref="TypeTraits"/>. The lists and traits are AND-ed, the
/// values within a list are OR-ed and an empty list or unset trait does not constrain.
/// </summary>
internal sealed class TypeSelector
{
    private readonly Glob[] _namespaces;
    private readonly Glob[] _assemblies;
    private readonly Glob[] _types;
    private readonly Glob[] _baseTypes;
    private readonly Glob[] _interfaces;
    private readonly Glob[] _attributes;
    private readonly AttributeArgumentMatcher[] _attributeArguments;
    private readonly Glob[] _onlyAttributes;
    private readonly Glob[] _members;
    private readonly TypeTraits _traits;

    private TypeSelector(
        Glob[] namespaces,
        Glob[] assemblies,
        Glob[] types,
        Glob[] baseTypes,
        Glob[] interfaces,
        Glob[] attributes,
        AttributeArgumentMatcher[] attributeArguments,
        Glob[] onlyAttributes,
        Glob[] members,
        TypeTraits traits)
    {
        _attributeArguments = attributeArguments;
        _onlyAttributes = onlyAttributes;
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
        && _baseTypes.Length == 0 && _interfaces.Length == 0 && !HasAttributeKeys && _traits.IsEmpty;

    private bool HasAttributeKeys => _attributes.Length > 0 || _attributeArguments.Length > 0 || _onlyAttributes.Length > 0;

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
            selector.AttributeArguments,
            selector.OnlyAttributes,
            selector.Members,
            TypeTraits.From(selector));
    }

    /// <summary>
    /// Compiles a rule exception once: its <c>namespaces</c>, <c>assemblies</c>, <c>types</c> and <c>attributes</c> globs, its
    /// <c>attributeArguments</c> and <c>onlyAttributes</c>, and its traits, all AND-ed. The loader validates patterns and
    /// values, so an invalid one is a programming error.
    /// </summary>
    /// <param name="exception">The exception to compile.</param>
    /// <returns>The selector.</returns>
    /// <exception cref="ArgumentException">A pattern or an accessibility is invalid.</exception>
    public static TypeSelector From(RuleException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Compile(
            exception.Namespaces,
            exception.Assemblies,
            exception.Types,
            [],
            [],
            exception.Attributes,
            exception.AttributeArguments,
            exception.OnlyAttributes,
            [],
            TypeTraits.From(exception));
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
        Compile(namespaces, assemblies, types, baseTypes, interfaces, attributes, [], [], members, TypeTraits.None);

    private static TypeSelector Compile(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string> types,
        IList<string> baseTypes,
        IList<string> interfaces,
        IList<string> attributes,
        IList<AttributeArgumentMatch> attributeArguments,
        IList<string> onlyAttributes,
        IList<string> members,
        TypeTraits traits)
    {
        ArgumentNullException.ThrowIfNull(attributeArguments);
        ArgumentNullException.ThrowIfNull(onlyAttributes);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(namespaces);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(baseTypes);
        ArgumentNullException.ThrowIfNull(interfaces);
        ArgumentNullException.ThrowIfNull(attributes);

        var matchers = new AttributeArgumentMatcher[attributeArguments.Count];
        for (var i = 0; i < matchers.Length; i++)
        {
            matchers[i] = AttributeArgumentMatcher.From(attributeArguments[i]);
        }

        return new TypeSelector(
            Glob.CompileAll(namespaces, Glob.Dotted),
            Glob.CompileAll(assemblies, Glob.Flat),
            Glob.CompileAll(types, Glob.Dotted),
            Glob.CompileAll(baseTypes, Glob.Dotted),
            Glob.CompileAll(interfaces, Glob.Dotted),
            Glob.CompileAll(attributes, Glob.Dotted),
            matchers,
            Glob.CompileAll(onlyAttributes, Glob.Dotted),
            Glob.CompileAll(members, Glob.Dotted),
            traits);
    }

    /// <summary>
    /// Whether the attribute class is one the compiler emits on its own, which <c>onlyAttributes</c> never counts. A source
    /// type never shows them through Roslyn, a metadata type does, so ignoring them gives one meaning for both.
    /// </summary>
    /// <param name="attributeClass">The attribute class.</param>
    /// <param name="names">The names of the check.</param>
    /// <returns>True for a compiler-emitted attribute class.</returns>
    internal static bool IsCompilerAttribute(INamedTypeSymbol attributeClass, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(attributeClass);
        ArgumentNullException.ThrowIfNull(names);

        return names.FullNameOf(attributeClass.OriginalDefinition) switch
        {
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute"
                or "System.Runtime.CompilerServices.NullableAttribute"
                or "System.Runtime.CompilerServices.NullableContextAttribute"
                or "System.Runtime.CompilerServices.NullablePublicOnlyAttribute"
                or "System.Runtime.CompilerServices.IsReadOnlyAttribute"
                or "System.Runtime.CompilerServices.IsByRefLikeAttribute"
                or "Microsoft.CodeAnalysis.EmbeddedAttribute" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Names the first attribute class applied directly to the type, in declaration order, that no <c>onlyAttributes</c>
    /// glob matches. Compiler-emitted attributes are skipped.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="names">The names of the check.</param>
    /// <returns>The full name of that attribute class, or <see langword="null"/> when every attribute is inside the set or the selector has no <c>onlyAttributes</c>.</returns>
    internal string? FirstOutsideOnlyAttributes(INamedTypeSymbol type, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(names);

        if (_onlyAttributes.Length == 0)
        {
            return null;
        }

        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass && IsOutsideOnlyAttributes(attributeClass, names))
            {
                return names.FullNameOf(attributeClass.OriginalDefinition);
            }
        }

        return null;
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

        // The traits that read only the type go before the base, interface and attribute walks; assignableTo walks last.
        var hasTraits = !_traits.IsEmpty;
        return (!hasTraits || _traits.FlagsMatch(type))
            && (_baseTypes.Length == 0 || AnyBaseMatches(type, names))
            && (_interfaces.Length == 0 || AnyInterfaceMatches(type, names))
            && (!HasAttributeKeys || AttributeKeysMatch(type, names))
            && (!hasTraits || _traits.AssignableMatches(type, names));
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

    // One pass over the attributes applied directly to the type serves attributes, attributeArguments and onlyAttributes.
    // attributes and attributeArguments each need some attribute to match; onlyAttributes needs every one inside its set.
    private bool AttributeKeysMatch(INamedTypeSymbol type, TypeNames names)
    {
        var attributeFound = _attributes.Length == 0;
        var argumentsFound = _attributeArguments.Length == 0;
        var checkOnly = _onlyAttributes.Length > 0;
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass)
            {
                if (checkOnly && IsOutsideOnlyAttributes(attributeClass, names))
                {
                    return false;
                }

                attributeFound = attributeFound || Glob.AnyMatch(_attributes, names.FullNameOf(attributeClass.OriginalDefinition));
            }

            argumentsFound = argumentsFound || AnyArgumentEntryMatches(attribute, names);
            if (attributeFound && argumentsFound && !checkOnly)
            {
                return true;
            }
        }

        return attributeFound && argumentsFound;
    }

    private bool IsOutsideOnlyAttributes(INamedTypeSymbol attributeClass, TypeNames names) =>
        !IsCompilerAttribute(attributeClass, names) && !Glob.AnyMatch(_onlyAttributes, names.FullNameOf(attributeClass.OriginalDefinition));

    private bool AnyArgumentEntryMatches(AttributeData attribute, TypeNames names)
    {
        foreach (var matcher in _attributeArguments)
        {
            if (matcher.Matches(attribute, names))
            {
                return true;
            }
        }

        return false;
    }
}
