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

        var allowedDependencies = BuildAllowedDependencies(resolvedPack);

        foreach (var filePath in csFiles)
        {
            ValidateFile(filePath, knownLayers, allowedDependencies, violations);
        }

        return new SourceValidationResult
        {
            IsValid = violations.Count == 0,
            Violations = violations,
            FilesScanned = csFiles.Length,
        };
    }

    private static Dictionary<string, HashSet<string>> BuildAllowedDependencies(RulePack resolvedPack)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in resolvedPack.Layers)
        {
            result[layer.Name] = new HashSet<string>(layer.AllowedDependencies, StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static void ValidateFile(
        string filePath,
        HashSet<string> knownLayers,
        Dictionary<string, HashSet<string>> allowedDependencies,
        List<SourceViolation> violations)
    {
        var sourceText = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetCompilationUnitRoot();

        var namespaceName = ExtractNamespace(root);
        if (namespaceName is null)
        {
            return;
        }

        var sourceLayer = DetectLayer(namespaceName, knownLayers);
        if (sourceLayer is null)
        {
            return;
        }

        foreach (var usingDirective in root.Usings)
        {
            var usingName = usingDirective.Name?.ToString();
            if (usingName is null)
            {
                continue;
            }

            var targetLayer = DetectLayer(usingName, knownLayers);
            if (targetLayer is null || string.Equals(targetLayer, sourceLayer, StringComparison.OrdinalIgnoreCase))
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
