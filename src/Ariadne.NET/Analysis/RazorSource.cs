using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Analysis;

/// <summary>Maps locations in a tree of the Razor source generator back to the <c>.razor</c> or <c>.cshtml</c> file it came from.</summary>
internal static class RazorSource
{
    private const string GeneratedSuffix = ".g.cs";
    private const string DeclarationSuffix = ".decl.g.cs";

    /// <summary>
    /// The Razor file of each tree: the file its <c>#pragma checksum</c> names; for a declaration tree (<c>X.decl.g.cs</c>),
    /// which has no pragma, the file of its sibling <c>X.g.cs</c>; otherwise the tree's own path. The first <c>#line</c> of a
    /// tree is not used: it usually maps the usings of <c>_Imports.razor</c>.
    /// </summary>
    public static Dictionary<SyntaxTree, string> FilePaths(IReadOnlySet<SyntaxTree> razorTrees, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(razorTrees);

        var files = new Dictionary<SyntaxTree, string>(razorTrees.Count);
        var byGeneratedPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tree in razorTrees)
        {
            if (ChecksumFile(tree, cancellationToken) is { } file)
            {
                files[tree] = file;
                byGeneratedPath[tree.FilePath] = file;
            }
        }

        foreach (var tree in razorTrees)
        {
            if (!files.ContainsKey(tree))
            {
                files[tree] = SiblingPath(tree.FilePath) is { } sibling && byGeneratedPath.TryGetValue(sibling, out var file)
                    ? file
                    : tree.FilePath;
            }
        }

        return files;
    }

    /// <summary>
    /// The location of a span: its <c>#line</c>-mapped position when it maps; otherwise line 1, column 1 of
    /// <paramref name="razorFilePath"/> when that names another file than the tree; otherwise the span's own position.
    /// </summary>
    public static SourceLocation Locate(SyntaxTree tree, TextSpan span, string razorFilePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(razorFilePath);

        var mapped = tree.GetMappedLineSpan(span, cancellationToken);
        if (mapped.HasMappedPath)
        {
            return new SourceLocation(mapped.Path, mapped.StartLinePosition.Line + 1, mapped.StartLinePosition.Character + 1);
        }

        if (!string.Equals(razorFilePath, tree.FilePath, StringComparison.Ordinal))
        {
            return new SourceLocation(razorFilePath, 1, 1);
        }

        var start = tree.GetLineSpan(span, cancellationToken).StartLinePosition;
        return new SourceLocation(tree.FilePath, start.Line + 1, start.Character + 1);
    }

    private static string? ChecksumFile(SyntaxTree tree, CancellationToken cancellationToken)
    {
        var root = (CSharpSyntaxNode)tree.GetRoot(cancellationToken);
        return root.GetFirstDirective(static d => d.IsKind(SyntaxKind.PragmaChecksumDirectiveTrivia)) is PragmaChecksumDirectiveTriviaSyntax
        {
            File.ValueText: { Length: > 0 } file,
        }
            ? file
            : null;
    }

    // X.decl.g.cs → X.g.cs, the tree the generator writes for the same Razor file; null for any other name.
    private static string? SiblingPath(string filePath) =>
        filePath.EndsWith(DeclarationSuffix, StringComparison.OrdinalIgnoreCase)
            ? string.Concat(filePath.AsSpan(0, filePath.Length - DeclarationSuffix.Length), GeneratedSuffix)
            : null;
}
