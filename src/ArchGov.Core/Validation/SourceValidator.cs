using ArchGov.Core.RuleSchema;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchGov.Core.Validation;

public sealed class SourceValidator : ISourceValidator
{
    public SourceValidationResult Validate(RulePack resolvedPack, string sourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(sourceDirectory);

        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
        var violations = new List<SourceViolation>();
        var knownLayers = new HashSet<string>(
            resolvedPack.Layers.Select(l => l.Name),
            StringComparer.OrdinalIgnoreCase);

        var allowedDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in resolvedPack.Layers)
        {
            allowedDependencies[layer.Name] = new HashSet<string>(layer.AllowedDependencies, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var filePath in csFiles)
        {
            var sourceText = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetCompilationUnitRoot();

            var namespaceName = ExtractNamespace(root);
            if (namespaceName is null)
            {
                continue;
            }

            var sourceLayer = DetectLayer(namespaceName, knownLayers);
            if (sourceLayer is null)
            {
                continue;
            }

            var usings = root.Usings;
            foreach (var usingDirective in usings)
            {
                var usingName = usingDirective.Name?.ToString();
                if (usingName is null)
                {
                    continue;
                }

                var targetLayer = DetectLayer(usingName, knownLayers);
                if (targetLayer is null)
                {
                    continue;
                }

                if (string.Equals(targetLayer, sourceLayer, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!allowedDependencies.TryGetValue(sourceLayer, out var allowed) || !allowed.Contains(targetLayer))
                {
                    violations.Add(new SourceViolation
                    {
                        FilePath = filePath,
                        ViolationType = "DependencyViolation",
                        Message = $"Layer '{sourceLayer}' is not allowed to depend on '{targetLayer}' (via using '{usingName}')",
                    });
                }
            }
        }

        return new SourceValidationResult
        {
            IsValid = violations.Count == 0,
            Violations = violations,
            FilesScanned = csFiles.Length,
        };
    }

    private static string? ExtractNamespace(CompilationUnitSyntax root)
    {
        var fileScopedNs = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNs is not null)
        {
            return fileScopedNs.Name.ToString();
        }

        var blockNs = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
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
}
