using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Analysis;

/// <summary>Binds the type names in hand-written syntax trees and records an edge from the enclosing type to each referenced type.</summary>
internal sealed class TypeReferenceWalker : CSharpSyntaxWalker
{
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<INamedTypeSymbol, bool> _checkedTypes = new(SymbolEqualityComparer.Default);
    private SemanticModel? _model;
    private INamedTypeSymbol? _programType;

    /// <summary>The current edge source, or null outside a type or inside a type that is not checked.</summary>
    private INamedTypeSymbol? _source;

    public TypeReferenceWalker(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

    /// <summary>The edges found so far; a set, so a repeated edge at one location is kept once.</summary>
    public HashSet<TypeEdge> Edges { get; } = [];

    /// <summary>Each checked type, with the location of its first declaration seen.</summary>
    public Dictionary<INamedTypeSymbol, SourceLocation> SourceTypes { get; } = new(SymbolEqualityComparer.Default);

    /// <summary>Walks one tree, binding through the given semantic model.</summary>
    public void Walk(SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;
        _programType = null;
        _source = null;
        Visit(model.SyntaxTree.GetRoot(_cancellationToken));
        _model = null;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previous = EnterType(node, node.Identifier);
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
        // is the only place that can record the types inside it.
        if (Model.GetAliasInfo(node, _cancellationToken) is { } alias)
        {
            AddEdgesForType(alias.Target as ITypeSymbol, node.Span);
            return;
        }

        var symbol = Resolve(Model.GetSymbolInfo(node, _cancellationToken));
        AddEdge(NormalizeSymbol(symbol), node.Span);

        // A bare name that binds into a constructed type, as through 'using static Helper<Store>;',
        // has its type arguments written in the directive, not here.
        if (IsBareName(node) && symbol?.ContainingType is { } containingType && IsConstructed(containingType))
        {
            AddEdgesForType(containingType, node.Span);
        }
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        if (_source is not null)
        {
            AddEdge(Model.GetSymbolInfo(node, _cancellationToken), node.Identifier.Span);
        }

        base.VisitGenericName(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        if (_source is not null)
        {
            AddEdge(Model.GetSymbolInfo(node, _cancellationToken), node.NewKeyword.Span);
        }

        base.VisitImplicitObjectCreationExpression(node);
    }

    private SemanticModel Model => _model ?? throw new InvalidOperationException("The walker is not walking a tree.");

    private static INamedTypeSymbol? NormalizeSymbol(ISymbol? symbol) => symbol switch
    {
        IAliasSymbol alias => NormalizeSymbol(alias.Target),
        IMethodSymbol method => NormalizeType((method.ReducedFrom ?? method).ContainingType),
        IPropertySymbol or IFieldSymbol or IEventSymbol => NormalizeType(symbol.ContainingType),
        ITypeSymbol type => NormalizeType(type),
        _ => null,
    };

    private static INamedTypeSymbol? NormalizeType(ITypeSymbol? type)
    {
        while (true)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    type = array.ElementType;
                    break;
                case IPointerTypeSymbol pointer:
                    type = pointer.PointedAtType;
                    break;
                case INamedTypeSymbol { IsExtension: true } extension:
                    type = extension.ContainingType;
                    break;
                case INamedTypeSymbol named when named.TypeKind != TypeKind.Error && !named.IsAnonymousType:
                    return named.OriginalDefinition;
                default:
                    return null;
            }
        }
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

    /// <summary>True when the name has no qualifier or receiver, so nothing at the use site writes its containing type.</summary>
    private static bool IsBareName(IdentifierNameSyntax node) => node.Parent switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name != node,
        QualifiedNameSyntax qualified => qualified.Right != node,
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name != node,
        MemberBindingExpressionSyntax => false,
        _ => true,
    };

    private static bool IsConstructed(INamedTypeSymbol type) =>
        !SymbolEqualityComparer.Default.Equals(type, type.OriginalDefinition);

    private void AddEdge(SymbolInfo info, TextSpan span) => AddEdge(NormalizeSymbol(Resolve(info)), span);

    private void AddEdge(INamedTypeSymbol? target, TextSpan span)
    {
        if (target is null || _source is null || SymbolEqualityComparer.Default.Equals(target, _source))
        {
            return;
        }

        Edges.Add(new TypeEdge(_source, target, ToLocation(span)));
    }

    /// <summary>Adds an edge to the type and to every named type inside it: element types, containing types and type arguments, recursively.</summary>
    private void AddEdgesForType(ITypeSymbol? type, TextSpan span)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                AddEdgesForType(array.ElementType, span);
                break;
            case IPointerTypeSymbol pointer:
                AddEdgesForType(pointer.PointedAtType, span);
                break;
            case INamedTypeSymbol named:
                AddEdge(NormalizeType(named), span);
                AddEdgesForType(named.ContainingType, span);
                foreach (var typeArgument in named.TypeArguments)
                {
                    AddEdgesForType(typeArgument, span);
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
