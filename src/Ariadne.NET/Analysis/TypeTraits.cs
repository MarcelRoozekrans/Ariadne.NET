using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The type-level traits of a selector: names, kinds, accessibility, assignability, <c>sealed</c>, <c>abstract</c>,
/// <c>static</c> and <c>nested</c>. Every trait that is set must match; the values within a list are OR-ed and an unset
/// trait does not constrain. Compiled once, matched without allocating.
/// </summary>
internal sealed class TypeTraits
{
    /// <summary>Traits that constrain nothing.</summary>
    public static readonly TypeTraits None = new([], [], [], [], sealedTrait: null, abstractTrait: null, staticTrait: null, nestedTrait: null);

    private readonly Glob[] _names;
    private readonly CSharpTypeKind[] _kinds;
    private readonly Accessibility[] _accessibility;
    private readonly Glob[] _assignableTo;
    private readonly bool? _sealed;
    private readonly bool? _abstract;
    private readonly bool? _static;
    private readonly bool? _nested;
    private readonly bool _isEmpty;

    private TypeTraits(
        Glob[] names,
        CSharpTypeKind[] kinds,
        Accessibility[] accessibility,
        Glob[] assignableTo,
        bool? sealedTrait,
        bool? abstractTrait,
        bool? staticTrait,
        bool? nestedTrait)
    {
        _names = names;
        _kinds = kinds;
        _accessibility = accessibility;
        _assignableTo = assignableTo;
        _sealed = sealedTrait;
        _abstract = abstractTrait;
        _static = staticTrait;
        _nested = nestedTrait;
        _isEmpty = names.Length == 0 && kinds.Length == 0 && accessibility.Length == 0 && assignableTo.Length == 0
            && sealedTrait is null && abstractTrait is null && staticTrait is null && nestedTrait is null;
    }

    /// <summary>Whether no trait is set, so the traits match every type. Computed once.</summary>
    public bool IsEmpty => _isEmpty;

    /// <summary>Compiles the traits of a selector once. The loader validates the values, so an invalid one is a programming error.</summary>
    /// <param name="selector">The selector whose trait members to compile.</param>
    /// <returns>The traits.</returns>
    /// <exception cref="ArgumentException">A name or type pattern is invalid, or an accessibility is not one of the six keywords.</exception>
    public static TypeTraits From(TargetSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Create(
            selector.Names,
            selector.Kinds,
            selector.Accessibility,
            selector.AssignableTo,
            selector.Sealed,
            selector.Abstract,
            selector.Static,
            selector.Nested);
    }

    /// <summary>Compiles the traits of a rule exception once. The loader validates the values, so an invalid one is a programming error.</summary>
    /// <param name="exception">The exception whose trait members to compile.</param>
    /// <returns>The traits.</returns>
    /// <exception cref="ArgumentException">A name or type pattern is invalid, or an accessibility is not one of the six keywords.</exception>
    public static TypeTraits From(RuleException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Create(
            exception.Names,
            exception.Kinds,
            exception.Accessibility,
            exception.AssignableTo,
            exception.Sealed,
            exception.Abstract,
            exception.Static,
            exception.Nested);
    }

    private static TypeTraits Create(
        IList<string> names,
        IList<CSharpTypeKind> kinds,
        IList<string> accessibilityKeywords,
        IList<string> assignableTo,
        bool? sealedTrait,
        bool? abstractTrait,
        bool? staticTrait,
        bool? nestedTrait)
    {
        var accessibility = new Accessibility[accessibilityKeywords.Count];
        for (var i = 0; i < accessibility.Length; i++)
        {
            accessibility[i] = ParseAccessibility(accessibilityKeywords[i]);
        }

        var traits = new TypeTraits(
            Glob.CompileAll(names, Glob.Flat),
            [.. kinds],
            accessibility,
            Glob.CompileAll(assignableTo, Glob.Dotted),
            sealedTrait,
            abstractTrait,
            staticTrait,
            nestedTrait);
        return traits.IsEmpty ? None : traits;
    }

    /// <summary>
    /// The declared kind of a type: <see cref="RoleRules.ActualKind"/> with enums and delegates told apart, which role
    /// checks fold into class.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The kind.</returns>
    public static CSharpTypeKind KindOf(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.TypeKind switch
        {
            TypeKind.Enum => CSharpTypeKind.Enum,
            TypeKind.Delegate => CSharpTypeKind.Delegate,
            _ => RoleRules.ActualKind(type),
        };
    }

    /// <summary>Whether the type matches every trait that is set. Ordinal and case-sensitive.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="names">The names of the check, so each type's full name is built once.</param>
    /// <returns>True when the type matches.</returns>
    public bool Matches(INamedTypeSymbol type, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(names);

        return FlagsMatch(type) && AssignableMatches(type, names);
    }

    /// <summary>
    /// Whether the type matches every set trait that reads only the type itself: everything but <c>assignableTo</c>, which
    /// walks the base chain and interfaces. Callers test this before their own walks, as it is cheap.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when those traits match.</returns>
    public bool FlagsMatch(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // The symbol flags first, the name glob last.
        return NestedMatches(type)
            && StaticMatches(type)
            && SealedMatches(type)
            && AbstractMatches(type)
            && KindMatches(type)
            && AccessibilityMatches(type)
            && NameMatches(type);
    }

    /// <summary>Whether a <c>names</c> glob matches the innermost simple name, arity left out. True when <c>names</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool NameMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _names.Length == 0 || Glob.AnyMatch(_names, type.Name);
    }

    /// <summary>Whether the type's declared kind is one of <c>kinds</c>. True when <c>kinds</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool KindMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_kinds.Length == 0)
        {
            return true;
        }

        var kind = KindOf(type);
        foreach (var wanted in _kinds)
        {
            if (wanted == kind)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the type's declared accessibility is one of <c>accessibility</c>. True when <c>accessibility</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool AccessibilityMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (_accessibility.Length == 0)
        {
            return true;
        }

        var declared = type.DeclaredAccessibility;
        foreach (var wanted in _accessibility)
        {
            if (wanted == declared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an <c>assignableTo</c> pattern matches the type itself, a class in its base chain, <c>System.Object</c>
    /// included, or an interface it implements. Generic types match by name without arity. True when <c>assignableTo</c> is unset.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="names">The names of the check.</param>
    /// <returns>True when the trait matches.</returns>
    public bool AssignableMatches(INamedTypeSymbol type, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(names);
        if (_assignableTo.Length == 0)
        {
            return true;
        }

        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (Glob.AnyMatch(_assignableTo, names.FullNameOf(current.OriginalDefinition)))
            {
                return true;
            }
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (Glob.AnyMatch(_assignableTo, names.FullNameOf(implemented.OriginalDefinition)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the type is sealed as <c>sealed</c> asks: a class or record class declared sealed, or a static class. True when <c>sealed</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool SealedMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _sealed is not { } wanted || wanted == (type.TypeKind == TypeKind.Class && (type.IsSealed || type.IsStatic));
    }

    /// <summary>Whether the type is abstract as <c>abstract</c> asks: a class or record class declared abstract, never a static class. True when <c>abstract</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool AbstractMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _abstract is not { } wanted || wanted == (type.TypeKind == TypeKind.Class && type.IsAbstract && !type.IsStatic);
    }

    /// <summary>Whether the type is a static class as <c>static</c> asks. True when <c>static</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool StaticMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _static is not { } wanted || wanted == (type.TypeKind == TypeKind.Class && type.IsStatic);
    }

    /// <summary>Whether the type is declared inside another type as <c>nested</c> asks. True when <c>nested</c> is unset.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the trait matches.</returns>
    public bool NestedMatches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _nested is not { } wanted || wanted == (type.ContainingType is not null);
    }

    private static Accessibility ParseAccessibility(string keyword) => keyword switch
    {
        "public" => Accessibility.Public,
        "internal" => Accessibility.Internal,
        "protected" => Accessibility.Protected,
        "private" => Accessibility.Private,
        "protected internal" => Accessibility.ProtectedOrInternal,
        "private protected" => Accessibility.ProtectedAndInternal,
        _ => throw new ArgumentException($"'{keyword}' is not an accessibility; use public, internal, protected, private, protected internal or private protected.", nameof(keyword)),
    };
}
