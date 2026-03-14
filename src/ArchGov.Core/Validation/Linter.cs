using ArchGov.Core.RuleSchema;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ZInject;

namespace ArchGov.Core.Validation;

[Singleton]
public sealed class Linter : ILinter
{
    public LintResult Lint(RulePack resolvedPack, string sourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(sourceDirectory);

        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
        var violations = new List<LintViolation>();
        var knownLayers = new HashSet<string>(
            resolvedPack.Layers.Select(static l => l.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in csFiles)
        {
            CheckFile(filePath, knownLayers, violations);
        }

        return new LintResult
        {
            IsClean = violations.Count == 0,
            Violations = violations,
            FilesScanned = csFiles.Length,
        };
    }

    private static void CheckFile(
        string filePath,
        HashSet<string> knownLayers,
        List<LintViolation> violations)
    {
        var sourceText = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();

        var namespaceName = ExtractNamespace(root);
        if (namespaceName is null)
        {
            return;
        }

        var namespaceLayer = DetectLayer(namespaceName, knownLayers);
        var directoryLayer = DetectLayerFromPath(filePath, knownLayers);

        if (namespaceLayer is null && directoryLayer is null)
        {
            violations.Add(new LintViolation
            {
                FilePath = filePath,
                Category = "UnrecognizedLayer",
                Message = $"No recognized layer found in namespace '{namespaceName}' or file path",
            });
            return;
        }

        if (namespaceLayer is not null
            && directoryLayer is not null
            && !string.Equals(namespaceLayer, directoryLayer, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(new LintViolation
            {
                FilePath = filePath,
                Category = "NamespaceMismatch",
                Message = $"File is in a directory suggesting layer '{directoryLayer}' "
                        + $"but namespace '{namespaceName}' suggests layer '{namespaceLayer}'",
            });
        }
    }

    private static string? ExtractNamespace(CompilationUnitSyntax root)
    {
        var fileScopedNs = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (fileScopedNs is not null)
        {
            return fileScopedNs.Name.ToString();
        }

        var blockNs = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return blockNs?.Name.ToString();
    }

    private static string? DetectLayer(string namespaceName, HashSet<string> knownLayers)
    {
        var segments = namespaceName.Split('.');
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            if (knownLayers.Contains(segments[i]))
            {
                return segments[i];
            }
        }

        return null;
    }

    private static string? DetectLayerFromPath(string filePath, HashSet<string> knownLayers)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var segments = normalizedPath.Split('/');
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var dirParts = segments[i].Split('.');
            foreach (var part in dirParts)
            {
                if (knownLayers.Contains(part))
                {
                    return part;
                }
            }
        }

        return null;
    }
}
