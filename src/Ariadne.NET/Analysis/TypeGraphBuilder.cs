using System.Collections.Frozen;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Builds the type dependency graph of a compilation's hand-written code.</summary>
internal static class TypeGraphBuilder
{
    /// <summary>
    /// Walks every non-generated tree of the compilation and collects its type edges and source types, and no member edges.
    /// Razor generator trees are skipped here, as they are in every tree set; pass them to the overload that takes them.
    /// </summary>
    public static TypeGraph Build(
        Compilation compilation,
        IReadOnlySet<SyntaxTree> generatedTrees,
        CancellationToken cancellationToken) =>
        Build(compilation, generatedTrees, FrozenSet<SyntaxTree>.Empty, MemberNameFilter.Empty, cancellationToken);

    /// <summary>
    /// Walks every non-generated tree of the compilation and collects its type edges, source types and the member uses whose
    /// name passes the filter.
    /// </summary>
    public static TypeGraph Build(
        Compilation compilation,
        IReadOnlySet<SyntaxTree> generatedTrees,
        MemberNameFilter filter,
        CancellationToken cancellationToken) =>
        Build(compilation, generatedTrees, FrozenSet<SyntaxTree>.Empty, filter, cancellationToken);

    /// <summary>
    /// Walks every non-generated tree of the compilation, and every tree of the Razor generator, which is checked as hand-written
    /// code with its locations mapped to the Razor file, and collects type edges, source types and the member uses whose name
    /// passes the filter.
    /// </summary>
    public static TypeGraph Build(
        Compilation compilation,
        IReadOnlySet<SyntaxTree> generatedTrees,
        IReadOnlySet<SyntaxTree> razorTrees,
        MemberNameFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(generatedTrees);
        ArgumentNullException.ThrowIfNull(razorTrees);
        ArgumentNullException.ThrowIfNull(filter);

        var walker = new TypeReferenceWalker(filter, cancellationToken);
        var razorFiles = razorTrees.Count == 0 ? null : RazorSource.FilePaths(razorTrees, cancellationToken);
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GeneratedCode.IsGeneratedTree(tree, compilation, generatedTrees, razorTrees, cancellationToken))
            {
                walker.Walk(compilation.GetSemanticModel(tree), razorFiles?.GetValueOrDefault(tree));
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

        return new TypeGraph(edges, SortedMemberEdges(walker.MemberEdges), sourceTypes, walker.SourceTypes);
    }

    private static IReadOnlyList<MemberEdge> SortedMemberEdges(HashSet<MemberEdge> found)
    {
        if (found.Count == 0)
        {
            return [];
        }

        var memberEdges = new List<MemberEdge>(found);
        memberEdges.Sort(static (x, y) => CompareMemberEdges(in x, in y));
        return memberEdges;
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

    private static int CompareMemberEdges(in MemberEdge x, in MemberEdge y)
    {
        var result = Compare(x.Location, y.Location);
        if (result == 0)
        {
            result = CompareNames(x.Source, y.Source);
        }

        return result == 0 ? string.CompareOrdinal(x.Member.ToDisplayString(), y.Member.ToDisplayString()) : result;
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
