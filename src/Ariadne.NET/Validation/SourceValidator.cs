using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using ZeroAlloc.Inject;

namespace Ariadne.NET.Validation;

[Singleton]
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

        // Collect matched roles across files (for interface requirement checks)
        var matchedRoles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in csFiles)
        {
            ValidateFile(filePath, knownLayers, allowedDependencies,
                resolvedPack.ComponentRoles, violations, matchedRoles);
        }

        // Second pass: check interface requirements
        CheckInterfaceRequirements(resolvedPack.ComponentRoles, matchedRoles, violations);

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
        IList<ComponentRoleDefinition> roles,
        List<SourceViolation> violations,
        Dictionary<string, List<string>> matchedRoles)
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

        ValidateTypeDeclarations(root, sourceLayer, roles, filePath, violations, matchedRoles);
    }

    private static void ValidateTypeDeclarations(
        CompilationUnitSyntax root,
        string sourceLayer,
        IList<ComponentRoleDefinition> roles,
        string filePath,
        List<SourceViolation> violations,
        Dictionary<string, List<string>> matchedRoles)
    {
        var typeDeclarations = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>();

        foreach (var typeDecl in typeDeclarations)
        {
            var typeName = typeDecl.Identifier.Text;
            var matchedRole = MatchTypeToRole(typeName, sourceLayer, roles);
            if (matchedRole is null)
            {
                continue;
            }

            if (!matchedRoles.TryGetValue(matchedRole.Name, out var typeNames))
            {
                typeNames = [];
                matchedRoles[matchedRole.Name] = typeNames;
            }

            typeNames.Add(typeName);

            CheckTypeKind(typeDecl, matchedRole, filePath, violations);
            CheckAccessibility(typeDecl, matchedRole, filePath, violations);
        }
    }

    private static ComponentRoleDefinition? MatchTypeToRole(
        string typeName,
        string layer,
        IList<ComponentRoleDefinition> roles)
    {
        foreach (var role in roles)
        {
            if (!string.Equals(role.Layer, layer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (typeName.EndsWith(role.Name, StringComparison.OrdinalIgnoreCase))
            {
                return role;
            }

            foreach (var alt in role.AlternativeNames)
            {
                if (typeName.EndsWith(alt, StringComparison.OrdinalIgnoreCase))
                {
                    return role;
                }
            }
        }

        return null;
    }

    private static CSharpTypeKind GetActualTypeKind(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl switch
        {
            RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                => CSharpTypeKind.RecordStruct,
            RecordDeclarationSyntax => CSharpTypeKind.Record,
            InterfaceDeclarationSyntax => CSharpTypeKind.Interface,
            StructDeclarationSyntax => CSharpTypeKind.Struct,
            _ => CSharpTypeKind.Class,
        };
    }

    private static void CheckAccessibility(
        TypeDeclarationSyntax typeDecl,
        ComponentRoleDefinition role,
        string filePath,
        List<SourceViolation> violations)
    {
        var expectedAccessibility = role.Accessibility;
        if (string.IsNullOrEmpty(expectedAccessibility) ||
            string.Equals(expectedAccessibility, "public", StringComparison.OrdinalIgnoreCase))
        {
            // Default is public — only report if role explicitly requires non-public
            return;
        }

        var isInternal = typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword));
        if (string.Equals(expectedAccessibility, "internal", StringComparison.OrdinalIgnoreCase) && !isInternal)
        {
            violations.Add(new SourceViolation
            {
                FilePath = filePath,
                ViolationType = "AccessibilityMismatch",
                Message = $"Type '{typeDecl.Identifier.Text}' matches role '{role.Name}' and should be '{expectedAccessibility}' but is declared 'public'",
            });
        }
    }

    private static void CheckTypeKind(
        TypeDeclarationSyntax typeDecl,
        ComponentRoleDefinition role,
        string filePath,
        List<SourceViolation> violations)
    {
        var actual = GetActualTypeKind(typeDecl);
        if (actual != role.TypeKind)
        {
            violations.Add(new SourceViolation
            {
                FilePath = filePath,
                ViolationType = "TypeKindMismatch",
                Message = $"Type '{typeDecl.Identifier.Text}' matches role '{role.Name}' and should be a '{role.TypeKind}' but is declared as a '{actual}'",
            });
        }
    }

    private static void CheckInterfaceRequirements(
        IList<ComponentRoleDefinition> roles,
        Dictionary<string, List<string>> matchedRoles,
        List<SourceViolation> violations)
    {
        foreach (var role in roles)
        {
            if (!role.RequiresInterface || string.IsNullOrEmpty(role.InterfaceRole))
            {
                continue;
            }

            // Check if any types were matched to the implementation role
            if (!matchedRoles.TryGetValue(role.Name, out var implTypes) || implTypes.Count == 0)
            {
                continue;
            }

            // Check if any types were matched to the interface role
            var hasInterfaceTypes = matchedRoles.TryGetValue(role.InterfaceRole, out var ifaceTypes)
                && ifaceTypes.Count > 0;

            if (!hasInterfaceTypes)
            {
                foreach (var implType in implTypes)
                {
                    violations.Add(new SourceViolation
                    {
                        FilePath = "",
                        ViolationType = "MissingInterface",
                        Message = $"Type '{implType}' matches role '{role.Name}' which requires interface role '{role.InterfaceRole}', but no matching interface was found",
                    });
                }
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
