using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Builds the type dependency graph of a compilation's hand-written code.</summary>
internal static class TypeGraphBuilder
{
    /// <summary>Walks every non-generated tree of the compilation and collects its type edges and source types.</summary>
    public static TypeGraph Build(
        Compilation compilation,
        IReadOnlySet<SyntaxTree> generatedTrees,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(generatedTrees);

        var walker = new TypeReferenceWalker(cancellationToken);
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GeneratedCode.IsGeneratedTree(tree, compilation, generatedTrees, cancellationToken))
            {
                walker.Walk(compilation.GetSemanticModel(tree));
            }
        }

        var edges = new List<TypeEdge>(walker.Edges);
        edges.Sort(static (x, y) => CompareEdges(in x, in y));

        var declarations = new List<(SourceLocation Location, INamedTypeSymbol Type)>(walker.SourceTypes.Count);
        foreach (var (type, location) in walker.SourceTypes)
        {
            declarations.Add((location, type));
        }

        declarations.Sort(static (x, y) => CompareDeclarations(x, y));
        var sourceTypes = new List<INamedTypeSymbol>(declarations.Count);
        foreach (ref readonly var declaration in CollectionsMarshal.AsSpan(declarations))
        {
            sourceTypes.Add(declaration.Type);
        }

        return new TypeGraph(edges, sourceTypes, walker.SourceTypes);
    }

    /// <summary>Orders locations by file path, line and column.</summary>
    internal static int Compare(in SourceLocation x, in SourceLocation y)
    {
        var result = string.CompareOrdinal(x.FilePath, y.FilePath);
        if (result == 0)
        {
            result = x.Line.CompareTo(y.Line);
        }

        return result == 0 ? x.Column.CompareTo(y.Column) : result;
    }

    private static int CompareEdges(in TypeEdge x, in TypeEdge y)
    {
        var result = Compare(x.Location, y.Location);
        if (result == 0)
        {
            result = CompareNames(x.Source, y.Source);
        }

        return result == 0 ? CompareNames(x.Target, y.Target) : result;
    }

    private static int CompareDeclarations(
        (SourceLocation Location, INamedTypeSymbol Type) x,
        (SourceLocation Location, INamedTypeSymbol Type) y)
    {
        var result = Compare(x.Location, y.Location);
        return result == 0 ? CompareNames(x.Type, y.Type) : result;
    }

    private static int CompareNames(INamedTypeSymbol x, INamedTypeSymbol y) =>
        string.CompareOrdinal(x.ToDisplayString(), y.ToDisplayString());
}
