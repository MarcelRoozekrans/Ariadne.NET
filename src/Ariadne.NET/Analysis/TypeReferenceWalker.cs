using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Binds the type names in hand-written syntax trees and records an edge from the enclosing type to each referenced type, and
/// a member edge for each member use whose name passes the <see cref="MemberNameFilter"/>.
/// </summary>
internal sealed class TypeReferenceWalker : CSharpSyntaxWalker
{
    private readonly CancellationToken _cancellationToken;
    private readonly MemberNameFilter _filter;

    /// <summary>Whether a constructor call could match, so an object creation is bound for its constructor at all.</summary>
    private readonly bool _constructorsCouldMatch;

    /// <summary>Whether an indexer use could match, so an element access is bound for its indexer at all.</summary>
    private readonly bool _indexersCouldMatch;
    private readonly Dictionary<INamedTypeSymbol, bool> _checkedTypes = new(SymbolEqualityComparer.Default);
    private SemanticModel? _model;
    private INamedTypeSymbol? _programType;

    /// <summary>The <c>using static</c> types in scope at <see cref="_importScopeOwner"/>, computed on first need.</summary>
    private List<INamedTypeSymbol>? _staticImports;
    private SyntaxNode? _importScopeOwner;

    /// <summary>The current edge source, or null outside a type or inside a type that is not checked.</summary>
    private INamedTypeSymbol? _source;

    public TypeReferenceWalker(MemberNameFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _filter = filter;
        _constructorsCouldMatch = filter.CouldMatch(MemberNames.ConstructorName);
        _indexersCouldMatch = filter.CouldMatchIndexer;
        _cancellationToken = cancellationToken;
    }

    /// <summary>The edges found so far; a set, so a repeated edge at one location is kept once.</summary>
    public HashSet<TypeEdge> Edges { get; } = [];

    /// <summary>The member uses found so far whose name passed the filter; a set, so a repeated use at one location is kept once.</summary>
    public HashSet<MemberEdge> MemberEdges { get; } = [];

    /// <summary>Each checked type, with the location of its first declaration seen.</summary>
    public Dictionary<INamedTypeSymbol, SourceLocation> SourceTypes { get; } = new(SymbolEqualityComparer.Default);

    /// <summary>Walks one tree, binding through the given semantic model.</summary>
    public void Walk(SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;
        _programType = null;
        _source = null;
        _staticImports = null;
        _importScopeOwner = null;
        Visit(model.SyntaxTree.GetRoot(_cancellationToken));
        _model = null;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
        AddImplicitBaseConstructorCalls(node);
        base.VisitClassDeclaration(node);
        _source = previous;
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
        base.VisitStructDeclaration(node);
        _source = previous;
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
        base.VisitInterfaceDeclaration(node);
        _source = previous;
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
        AddImplicitBaseConstructorCalls(node);
        base.VisitRecordDeclaration(node);
        _source = previous;
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
        base.VisitEnumDeclaration(node);
        _source = previous;
    }

    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
        base.VisitDelegateDeclaration(node);
        _source = previous;
    }

    // An extension block is not a type of its own here: its references belong to the enclosing static class.

    public override void VisitGlobalStatement(GlobalStatementSyntax node)
    {
        var previous = _source;
        _programType ??= Model.GetEnclosingSymbol(node.Statement.SpanStart, _cancellationToken)?.ContainingType;
        _source = _programType is { } program ? Enter(program, node.Statement.GetFirstToken().Span) : null;
        base.VisitGlobalStatement(node);
        _source = previous;
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_source is null || node.IsVar)
        {
            return;
        }

        // An alias target is written in the using directive, outside any type, so the use site
        // records the edges that writing the target in its place would give.
        if (Model.GetAliasInfo(node, _cancellationToken) is { } alias)
        {
            AddEdgesForWrittenType(alias.Target as ITypeSymbol, node.Span);
            return;
        }

        var info = Model.GetSymbolInfo(node, _cancellationToken);
        var symbol = Resolve(info);

        // Before the type edge, which a member of a written type such as 'Make' in 'Store.Make()' does not give.
        AddMemberUses(info, node, node.Span);

        // 'nint' and 'nuint' are keywords that parse as identifiers; like 'var' and like 'int', they give no edge.
        if (symbol is INamedTypeSymbol { IsNativeIntegerType: true } && node.Identifier.ValueText is "nint" or "nuint")
        {
            return;
        }

        var target = NormalizeSymbol(symbol);
        if (target is null || IsMemberOfWrittenType(node, symbol!, target))
        {
            return;
        }

        AddEdge(target, node.Span);

        // A bare name imported through 'using static Helper<Store>;' has its type written in the directive.
        if (FindStaticImport(node, symbol!) is { } imported)
        {
            AddEdgesForWrittenType(imported, node.Span);
        }
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        if (_source is not null)
        {
            var info = Model.GetSymbolInfo(node, _cancellationToken);
            AddEdge(NormalizeSymbol(Resolve(info)), node.Identifier.Span);
            AddMemberUses(info, node, node.Identifier.Span);
        }

        base.VisitGenericName(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        if (_source is not null)
        {
            var info = Model.GetSymbolInfo(node, _cancellationToken);
            AddEdge(NormalizeSymbol(Resolve(info)), node.NewKeyword.Span);
            AddMemberUses(info, node, node.NewKeyword.Span);
        }

        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        // The written type name gives the type edge; the constructor is bound only when a constructor pattern could match.
        if (_source is not null && _constructorsCouldMatch)
        {
            AddMemberUses(Model.GetSymbolInfo(node, _cancellationToken), node, node.NewKeyword.Span);
        }

        base.VisitObjectCreationExpression(node);
    }

    public override void VisitConstructorInitializer(ConstructorInitializerSyntax node)
    {
        if (_source is not null && _constructorsCouldMatch)
        {
            AddMemberUses(Model.GetSymbolInfo(node, _cancellationToken), node, node.ThisOrBaseKeyword.Span);
        }

        base.VisitConstructorInitializer(node);
    }

    public override void VisitPrimaryConstructorBaseType(PrimaryConstructorBaseTypeSyntax node)
    {
        if (_source is not null && _constructorsCouldMatch)
        {
            AddMemberUses(Model.GetSymbolInfo(node, _cancellationToken), node, node.Type.Span);
        }

        base.VisitPrimaryConstructorBaseType(node);
    }

    /// <summary>
    /// Records the base constructor call that C# inserts when a class or record constructor names no base arguments: an implicit
    /// constructor, a declared one without a <c>: base(...)</c> or <c>: this(...)</c> initializer, and a primary constructor
    /// whose base type has no argument list. The call goes to the base constructor that takes no arguments, the parameterless
    /// one or else one whose parameters are all optional or <c>params</c>. It is located at the constructor that makes it: the
    /// identifier of a declared constructor, or the type's identifier for an implicit or primary constructor. A struct has no
    /// base constructor call, a static constructor calls none, and a call to <see cref="object"/> is not recorded.
    /// </summary>
    private void AddImplicitBaseConstructorCalls(TypeDeclarationSyntax node)
    {
        if (_source is not { TypeKind: TypeKind.Class, IsStatic: false } type
            || !_constructorsCouldMatch
            || type.BaseType is not { SpecialType: not SpecialType.System_Object, TypeKind: not TypeKind.Error } baseType
            || ParameterlessConstructorOf(baseType) is not { } baseConstructor)
        {
            return;
        }

        if (node.ParameterList is not null)
        {
            if (!HasBaseArguments(node))
            {
                AddMemberUse(baseConstructor, node, node.Identifier.Span);
            }

            return;
        }

        foreach (var member in node.Members)
        {
            if (member is ConstructorDeclarationSyntax { Initializer: null } constructor
                && !constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                AddMemberUse(baseConstructor, constructor, constructor.Identifier.Span);
            }
        }

        if (HasImplicitConstructor(type) && IsFirstDeclaration(type, node))
        {
            AddMemberUse(baseConstructor, node, node.Identifier.Span);
        }
    }

    private static bool HasBaseArguments(TypeDeclarationSyntax node)
    {
        if (node.BaseList is { } baseList)
        {
            foreach (var baseType in baseList.Types)
            {
                if (baseType is PrimaryConstructorBaseTypeSyntax)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The default constructor C# declares for a class without constructors; a record's copy constructor takes an argument.
    private static bool HasImplicitConstructor(INamedTypeSymbol type)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor is { IsImplicitlyDeclared: true, Parameters.Length: 0 })
            {
                return true;
            }
        }

        return false;
    }

    // A partial class has one implicit constructor, recorded at its first declaration only.
    private static bool IsFirstDeclaration(INamedTypeSymbol type, SyntaxNode node) =>
        type.DeclaringSyntaxReferences is [var first, ..] && first.SyntaxTree == node.SyntaxTree && first.Span == node.Span;

    // The constructor a call without arguments binds to: the parameterless one, or else one whose parameters all have defaults or are params.
    private static IMethodSymbol? ParameterlessConstructorOf(INamedTypeSymbol type)
    {
        IMethodSymbol? fallback = null;
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters.IsEmpty)
            {
                return constructor;
            }

            if (fallback is null && constructor.Parameters.All(p => p.IsOptional || p.IsParams))
            {
                fallback = constructor;
            }
        }

        return fallback;
    }

    // An array element access binds to no member, so only an indexer gives a member edge.
    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        if (_source is not null && _indexersCouldMatch)
        {
            AddMemberUses(Model.GetSymbolInfo(node, _cancellationToken), node, node.ArgumentList.Span);
        }

        base.VisitElementAccessExpression(node);
    }

    public override void VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        if (_source is not null && _indexersCouldMatch)
        {
            AddMemberUses(Model.GetSymbolInfo(node, _cancellationToken), node, node.ArgumentList.Span);
        }

        base.VisitElementBindingExpression(node);
    }

    private SemanticModel Model => _model ?? throw new InvalidOperationException("The walker is not walking a tree.");

    private static INamedTypeSymbol? NormalizeSymbol(ISymbol? symbol) => symbol switch
    {
        IMethodSymbol method => NormalizeType((method.ReducedFrom ?? method).ContainingType),
        IPropertySymbol or IFieldSymbol or IEventSymbol => NormalizeType(symbol.ContainingType),
        ITypeSymbol type => NormalizeType(type),
        _ => null,
    };

    private static INamedTypeSymbol? NormalizeType(ITypeSymbol? type)
    {
        // A member of a C# 14 extension block belongs to the static class around the block.
        if (type is INamedTypeSymbol { IsExtension: true } extension)
        {
            type = extension.ContainingType;
        }

        return type is INamedTypeSymbol named && named.TypeKind != TypeKind.Error && !named.IsAnonymousType
            ? named.OriginalDefinition
            : null;
    }

    /// <summary>
    /// True for a type C# writes as a keyword, such as <c>int</c>, <c>string</c> or <c>nint</c>. A written keyword gives no edge:
    /// most are predefined-type syntax, and a written <c>nint</c> or <c>nuint</c> is skipped in <see cref="VisitIdentifierName"/>.
    /// </summary>
    private static bool IsKeywordType(INamedTypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Object or SpecialType.System_Void or SpecialType.System_Boolean or SpecialType.System_Char
            or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Decimal or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_String => true,
        SpecialType.System_IntPtr or SpecialType.System_UIntPtr => type.IsNativeIntegerType,
        _ => false,
    };

    /// <summary>
    /// True when the name is a member accessed through a type name that already gave the edge, as <c>Count</c> in <c>Store.Count</c>,
    /// so one written reference counts once.
    /// </summary>
    private bool IsMemberOfWrittenType(IdentifierNameSyntax node, ISymbol symbol, INamedTypeSymbol target)
    {
        if (symbol is ITypeSymbol)
        {
            return false;
        }

        var left = node.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name == node => memberAccess.Expression,
            _ => null,
        };

        return left is not null
            && Resolve(Model.GetSymbolInfo(left, _cancellationToken)) is ITypeSymbol leftType
            && SymbolEqualityComparer.Default.Equals(NormalizeType(leftType), target);
    }

    /// <summary>
    /// For a bare name that binds to a static member or nested type, returns the <c>using static</c> type that imports it.
    /// That is the member's containing type itself: <c>using static</c> imports only members declared directly in the named type,
    /// never inherited ones. Null for any other name.
    /// </summary>
    private INamedTypeSymbol? FindStaticImport(IdentifierNameSyntax node, ISymbol symbol)
    {
        if (symbol.ContainingType is not { } containingType
            || (symbol is not INamedTypeSymbol && !symbol.IsStatic)
            || SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, _source)
            || node.Parent is MemberAccessExpressionSyntax { Name: var name } && name == node
            || node.Parent is QualifiedNameSyntax { Right: var right } && right == node
            || node.Parent is AliasQualifiedNameSyntax or MemberBindingExpressionSyntax)
        {
            return null;
        }

        foreach (ref readonly var imported in CollectionsMarshal.AsSpan(GetStaticImports(node)))
        {
            if (SymbolEqualityComparer.Default.Equals(imported, containingType))
            {
                return imported;
            }
        }

        return null;
    }

    private List<INamedTypeSymbol> GetStaticImports(SyntaxNode node)
    {
        // Import scopes change only at namespace declarations, so the list is cached per declaration.
        var owner = (SyntaxNode?)node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>() ?? node.SyntaxTree.GetRoot(_cancellationToken);
        if (_staticImports is not null && owner == _importScopeOwner)
        {
            return _staticImports;
        }

        var imports = new List<INamedTypeSymbol>();
        foreach (var scope in Model.GetImportScopes(node.SpanStart, _cancellationToken))
        {
            foreach (var import in scope.Imports)
            {
                if (import.NamespaceOrType is INamedTypeSymbol type)
                {
                    imports.Add(type);
                }
            }
        }

        _importScopeOwner = owner;
        _staticImports = imports;
        return imports;
    }

    private INamedTypeSymbol? EnterType(SyntaxNode declaration, SyntaxToken identifier)
    {
        // A local copy: calling the method on the readonly field makes a hidden copy, which EPS06 rejects.
        var cancellationToken = _cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        var previous = _source;
        _source = Model.GetDeclaredSymbol(declaration, _cancellationToken) is INamedTypeSymbol type
            ? Enter(type, identifier.Span)
            : null;
        return previous;
    }

    /// <summary>Records the type as a source and returns it, or returns null when the type is not checked.</summary>
    private INamedTypeSymbol? Enter(INamedTypeSymbol type, TextSpan declarationSpan)
    {
        if (!IsChecked(type))
        {
            return null;
        }

        var location = ToLocation(declarationSpan);
        if (!SourceTypes.TryGetValue(type, out var known) || TypeGraphBuilder.Compare(location, known) < 0)
        {
            SourceTypes[type] = location;
        }

        return type;
    }

    /// <summary>A type is checked unless it, or a type it is nested in, carries <c>[GeneratedCode]</c>.</summary>
    private bool IsChecked(INamedTypeSymbol type)
    {
        if (_checkedTypes.TryGetValue(type, out var isChecked))
        {
            return isChecked;
        }

        isChecked = !GeneratedCode.HasGeneratedCodeAttribute(type)
            && (type.ContainingType is not { } outer || IsChecked(outer));
        _checkedTypes[type] = isChecked;
        return isChecked;
    }

    private static ISymbol? Resolve(SymbolInfo info) =>
        info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);

    /// <summary>
    /// Records the member use of the bound symbol or, when binding found no single symbol (a call with a dynamic argument, an
    /// ambiguity), of every candidate: the edge set de-duplicates, and the candidates share a simple name. With an empty filter
    /// it returns at once, so a pack without <c>members</c> patterns pays nothing here.
    /// </summary>
    private void AddMemberUses(SymbolInfo info, SyntaxNode node, TextSpan span)
    {
        if (_filter.IsEmpty)
        {
            return;
        }

        if (info.Symbol is { } symbol)
        {
            AddMemberUse(symbol, node, span);
            return;
        }

        foreach (var candidate in info.CandidateSymbols)
        {
            AddMemberUse(candidate, node, span);
        }
    }

    /// <summary>Records a member edge when the symbol is a member whose simple name passes the filter, and the name is not inside a <c>nameof</c>.</summary>
    /// <remarks>
    /// A name that binds to a constructor is an attribute name, and an attribute constructor is not a use. An indexer skips the
    /// name filter: C# matches an indexer to the interface indexer it implements by signature, not by name, so an indexer
    /// named <c>Chars</c> can implement one named <c>Item</c>, and only its chain shows that name. Element accesses are bound
    /// whenever the filter is not empty, so this adds no binding.
    /// </remarks>
    private void AddMemberUse(ISymbol symbol, SyntaxNode node, TextSpan span)
    {
        if (_source is null
            || (node is SimpleNameSyntax && symbol is IMethodSymbol { MethodKind: MethodKind.Constructor })
            || MemberNames.UsedMember(symbol) is not { } member
            || (member is not IPropertySymbol { IsIndexer: true } && !_filter.CouldMatch(MemberNames.SimpleNameOf(member)))
            || IsInsideNameOf(node))
        {
            return;
        }

        MemberEdges.Add(new MemberEdge(_source, member, ToLocation(span)));
    }

    /// <summary>True when the node is part of the argument of a <c>nameof</c>, which names a member without using it.</summary>
    private bool IsInsideNameOf(SyntaxNode node)
    {
        for (var current = node.Parent; current is ExpressionSyntax or ArgumentSyntax or ArgumentListSyntax; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } name } invocation
                && invocation.ArgumentList.Span.Contains(node.Span)
                && Model.GetSymbolInfo(name, _cancellationToken).Symbol is null)
            {
                return true;
            }
        }

        return false;
    }

    private void AddEdge(INamedTypeSymbol? target, TextSpan span)
    {
        if (target is null || _source is null || SymbolEqualityComparer.Default.Equals(target, _source))
        {
            return;
        }

        Edges.Add(new TypeEdge(_source, target, ToLocation(span)));
    }

    /// <summary>
    /// Adds the edges that writing the type at the span would give: the type and its containing types, the type arguments,
    /// tuple element, array and pointer element types, and function pointer signatures, recursively.
    /// A tuple or <c>T?</c> gives only its parts, and a keyword type gives nothing, as their written forms do.
    /// </summary>
    private void AddEdgesForWrittenType(ITypeSymbol? type, TextSpan span)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                AddEdgesForWrittenType(array.ElementType, span);
                break;
            case IPointerTypeSymbol pointer:
                AddEdgesForWrittenType(pointer.PointedAtType, span);
                break;
            case IFunctionPointerTypeSymbol functionPointer:
                AddEdgesForWrittenType(functionPointer.Signature.ReturnType, span);
                foreach (var parameter in functionPointer.Signature.Parameters)
                {
                    AddEdgesForWrittenType(parameter.Type, span);
                }

                break;
            case INamedTypeSymbol { IsTupleType: true } tuple:
                foreach (var element in tuple.TupleElements)
                {
                    AddEdgesForWrittenType(element.Type, span);
                }

                break;
            case INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable:
                AddEdgesForWrittenType(nullable.TypeArguments[0], span);
                break;
            case INamedTypeSymbol named when !IsKeywordType(named):
                AddEdge(NormalizeType(named), span);
                AddEdgesForWrittenType(named.ContainingType, span);
                foreach (var typeArgument in named.TypeArguments)
                {
                    AddEdgesForWrittenType(typeArgument, span);
                }

                break;
        }
    }

    private SourceLocation ToLocation(TextSpan span)
    {
        var tree = Model.SyntaxTree;
        var start = tree.GetLineSpan(span, _cancellationToken).StartLinePosition;
        return new SourceLocation(tree.FilePath, start.Line + 1, start.Character + 1);
    }
}
