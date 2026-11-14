using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Analysis;

/// <summary>Maps locations in a tree of the Razor source generator back to the <c>.razor</c> or <c>.cshtml</c> file it came from.</summary>
internal static class RazorSource
{
    /// <summary>
    /// The Razor file a generated tree came from: the path of its first <c>#line</c> mapping, else the file its
    /// <c>#pragma checksum</c> names, else null.
    /// </summary>
    public static string? FilePathOf(SyntaxTree tree, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tree);

        foreach (var mapping in tree.GetLineMappings(cancellationToken))
        {
            if (mapping.MappedSpan.HasMappedPath)
            {
                return mapping.MappedSpan.Path;
            }
        }

        var root = (CSharpSyntaxNode)tree.GetRoot(cancellationToken);
        return root.GetFirstDirective(static d => d.IsKind(SyntaxKind.PragmaChecksumDirectiveTrivia)) is PragmaChecksumDirectiveTriviaSyntax
        {
            File.ValueText: { Length: > 0 } file,
        }
            ? file
            : null;
    }

    /// <summary>
    /// The location of a span: its <c>#line</c>-mapped position when it maps; otherwise line 1, column 1 of
    /// <paramref name="fallbackPath"/> when that names another file than the tree; otherwise the span's own position.
    /// </summary>
    public static SourceLocation Locate(SyntaxTree tree, TextSpan span, string fallbackPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(fallbackPath);

        var mapped = tree.GetMappedLineSpan(span, cancellationToken);
        if (mapped.HasMappedPath)
        {
            return new SourceLocation(mapped.Path, mapped.StartLinePosition.Line + 1, mapped.StartLinePosition.Character + 1);
        }

        if (!string.Equals(fallbackPath, tree.FilePath, StringComparison.Ordinal))
        {
            return new SourceLocation(fallbackPath, 1, 1);
        }

        var start = tree.GetLineSpan(span, cancellationToken).StartLinePosition;
        return new SourceLocation(tree.FilePath, start.Line + 1, start.Character + 1);
    }
}
