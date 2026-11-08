using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The names a member use is matched by: <c>Namespace.Type.Member</c> of the original definition, with the type named as
/// <see cref="TypeNames.FullNameOf"/> names it, so nested types are joined with dots and carry no arity.
/// </summary>
internal static class MemberNames
{
    /// <summary>The member name of a constructor. <c>new</c> is a C# keyword, so no other member can carry it.</summary>
    public const string ConstructorName = "new";

    /// <summary>
    /// The member a name binds to, as a member use records it: the original definition, with an extension method call reduced
    /// to its static method and a tuple element to its <c>ItemN</c> field. Null for anything that is not a member a use can
    /// name: types, namespaces, locals, parameters, local functions, lambdas, and members of anonymous or error types.
    /// </summary>
    /// <param name="symbol">The bound symbol.</param>
    /// <returns>The used member, or null.</returns>
    public static ISymbol? UsedMember(ISymbol? symbol)
    {
        ISymbol? member = symbol switch
        {
            IMethodSymbol method when IsNamedMethod(method) => (method.ReducedFrom ?? method).OriginalDefinition,
            IPropertySymbol property => property.OriginalDefinition,
            IFieldSymbol field => (field.CorrespondingTupleField ?? field).OriginalDefinition,
            IEventSymbol @event => @event.OriginalDefinition,
            _ => null,
        };

        return member?.ContainingType is { IsAnonymousType: false, TypeKind: not TypeKind.Error } ? member : null;
    }

    /// <summary>
    /// The member's simple name: <see cref="ConstructorName"/> for a constructor; for an indexer its metadata name, <c>Item</c>
    /// unless <c>[IndexerName]</c> renames it; for a C# explicit interface implementation, whose own name is qualified with the
    /// interface, the simple name of the interface member. Allocates nothing.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The simple name.</returns>
    public static string SimpleNameOf(ISymbol member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor })
        {
            return ConstructorName;
        }

        // The dot test comes first: it is cheap, and only an explicit implementation has a dotted name.
        if (member.Name.Contains('.', StringComparison.Ordinal) && ExplicitImplementations(member) is [var implemented, ..])
        {
            return SimpleNameOf(implemented);
        }

        return member is IPropertySymbol { IsIndexer: true } ? member.MetadataName : member.Name;
    }

    /// <summary>The member's <c>Namespace.Type.Member</c> name. A member of a C# 14 extension block belongs to the static class around the block.</summary>
    /// <param name="member">The member, as its original definition.</param>
    /// <param name="names">The type name cache of the check.</param>
    /// <returns>The display name.</returns>
    public static string DisplayNameOf(ISymbol member, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(names);

        var type = member.ContainingType ?? throw new ArgumentException("The symbol is not a member of a type.", nameof(member));
        if (type is { IsExtension: true, ContainingType: { } outer })
        {
            type = outer;
        }

        return string.Concat(names.FullNameOf(type.OriginalDefinition), ".", SimpleNameOf(member));
    }

    /// <summary>
    /// The used member's name followed by the name of every member it overrides or implements, upwards only and each once.
    /// A call through an interface member therefore does not gain the names of the classes that implement it.
    /// </summary>
    /// <param name="member">The used member, as a <see cref="MemberEdge"/> holds it.</param>
    /// <returns>The names, the used member's first.</returns>
    public static IReadOnlyList<string> Chain(ISymbol member) => Chain(member, new TypeNames());

    /// <summary>The chain of <see cref="Chain(ISymbol)"/>, naming types through the check's cache.</summary>
    /// <param name="member">The used member, as a <see cref="MemberEdge"/> holds it.</param>
    /// <param name="names">The type name cache of the check.</param>
    /// <returns>The names, the used member's first.</returns>
    public static IReadOnlyList<string> Chain(ISymbol member, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(names);

        var start = UsedMember(member) ?? throw new ArgumentException("The symbol is not a member a use can name.", nameof(member));
        var chain = new List<string>();
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var pending = new Queue<ISymbol>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            var name = DisplayNameOf(current, names);
            if (!chain.Contains(name, StringComparer.Ordinal))
            {
                chain.Add(name);
            }

            if (OverriddenMember(current) is { } overridden)
            {
                pending.Enqueue(overridden.OriginalDefinition);
            }

            EnqueueImplementedMembers(current, pending);
        }

        return chain;
    }

    // A method a name can bind to; local functions, lambdas, accessors and operators are not named by a use.
    private static bool IsNamedMethod(IMethodSymbol method) => method.MethodKind
        is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.ReducedExtension
        or MethodKind.DelegateInvoke or MethodKind.ExplicitInterfaceImplementation;

    private static ISymbol? OverriddenMember(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    private static ImmutableArray<ISymbol> ExplicitImplementations(ISymbol member) => member switch
    {
        IMethodSymbol method => ImmutableArray<ISymbol>.CastUp(method.ExplicitInterfaceImplementations),
        IPropertySymbol property => ImmutableArray<ISymbol>.CastUp(property.ExplicitInterfaceImplementations),
        IEventSymbol @event => ImmutableArray<ISymbol>.CastUp(@event.ExplicitInterfaceImplementations),
        _ => [],
    };

    // The interface members the member implements in its own type, explicitly or implicitly. An implicit implementation
    // has the interface member's name, so only the interface members of that name are tried.
    private static void EnqueueImplementedMembers(ISymbol member, Queue<ISymbol> pending)
    {
        foreach (var implemented in ExplicitImplementations(member))
        {
            pending.Enqueue(implemented.OriginalDefinition);
        }

        if (member is IFieldSymbol or IMethodSymbol { MethodKind: MethodKind.Constructor }
            || member.ContainingType is not { TypeKind: not TypeKind.Interface } type)
        {
            return;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            foreach (var candidate in @interface.GetMembers(member.Name))
            {
                if (candidate.Kind == member.Kind
                    && type.FindImplementationForInterfaceMember(candidate) is { } implementation
                    && SymbolEqualityComparer.Default.Equals(implementation.OriginalDefinition, member))
                {
                    pending.Enqueue(candidate.OriginalDefinition);
                }
            }
        }
    }
}
