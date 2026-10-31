using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using ZeroAlloc.Inject;

namespace Ariadne.NET.CodeGeneration;

[Singleton]
public sealed class CodeGeneratorService : ICodeGeneratorService
{
    public CodeGenerationResult Generate(RulePack resolvedPack, CodeGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(request);

        ComponentRoleDefinition? role = null;
        foreach (var r in resolvedPack.ComponentRoles)
        {
            if (string.Equals(r.Name, request.RoleName, StringComparison.OrdinalIgnoreCase))
            {
                role = r;
                break;
            }
        }

        if (role is null)
        {
            throw new ArgumentException(
                $"Role '{request.RoleName}' not found in rule pack '{resolvedPack.Name}'.",
                nameof(request));
        }

        var resolvedNamespace = ResolveTemplate(
            resolvedPack.NamingConventions.Namespace,
            request,
            role.Layer);

        var resolvedFilePath = ResolveTemplate(
            resolvedPack.NamingConventions.FilePath,
            request,
            role.Layer);

        var typeName = DetermineTypeName(request.ComponentName, role.TypeKind);

        var typeDeclaration = CreateTypeDeclaration(typeName, role, resolvedPack);

        var namespaceDeclaration = SyntaxFactory.FileScopedNamespaceDeclaration(
            SyntaxFactory.ParseName(resolvedNamespace))
            .AddMembers(typeDeclaration);

        var compilationUnit = SyntaxFactory.CompilationUnit()
            .AddMembers(namespaceDeclaration)
            .NormalizeWhitespace();

        using var workspace = new AdhocWorkspace();
        var formattedNode = Formatter.Format(compilationUnit, workspace);
        var code = formattedNode.ToFullString();

        return new CodeGenerationResult
        {
            FilePath = resolvedFilePath,
            Code = code,
            Namespace = resolvedNamespace,
            TypeName = typeName,
        };
    }

    private static string ResolveTemplate(string template, CodeGenerationRequest request, string layer)
    {
        return template
            .Replace("{SolutionName}", request.SolutionName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Feature}", request.Feature, StringComparison.OrdinalIgnoreCase)
            .Replace("{Layer}", layer, StringComparison.OrdinalIgnoreCase)
            .Replace("{ComponentName}", request.ComponentName, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineTypeName(string componentName, CSharpTypeKind typeKind)
    {
        if (typeKind == CSharpTypeKind.Interface
            && !componentName.StartsWith('I')
            )
        {
            return "I" + componentName;
        }

        return componentName;
    }

    private static MemberDeclarationSyntax CreateTypeDeclaration(
        string typeName,
        ComponentRoleDefinition role,
        RulePack pack)
    {
        var accessibilityTokens = ParseAccessibility(role.Accessibility);

        BaseListSyntax? baseList = null;
        if (role.RequiresInterface && !string.IsNullOrEmpty(role.InterfaceRole))
        {
            var interfaceTypeName = ResolveInterfaceTypeName(typeName, role.InterfaceRole, pack);
            baseList = SyntaxFactory.BaseList(
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                    SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.ParseTypeName(interfaceTypeName))));
        }

        return role.TypeKind switch
        {
            CSharpTypeKind.Class => CreateClassDeclaration(typeName, accessibilityTokens, baseList),
            CSharpTypeKind.Interface => CreateInterfaceDeclaration(typeName, accessibilityTokens, baseList),
            CSharpTypeKind.Record => CreateRecordDeclaration(typeName, accessibilityTokens, baseList),
            CSharpTypeKind.Struct => CreateStructDeclaration(typeName, accessibilityTokens, baseList),
            CSharpTypeKind.RecordStruct => CreateRecordStructDeclaration(typeName, accessibilityTokens, baseList),
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role.TypeKind,
                $"Unsupported type kind: {role.TypeKind}"),
        };
    }

    private static string ResolveInterfaceTypeName(
        string typeName,
        string interfaceRole,
        RulePack pack)
    {
        ComponentRoleDefinition? interfaceRoleDef = null;
        foreach (var r in pack.ComponentRoles)
        {
            if (string.Equals(r.Name, interfaceRole, StringComparison.OrdinalIgnoreCase))
            {
                interfaceRoleDef = r;
                break;
            }
        }

        if (interfaceRoleDef is not null
            && interfaceRoleDef.TypeKind == CSharpTypeKind.Interface)
        {
            // The interface type name: prepend I if not already
            if (!typeName.StartsWith('I'))
            {
                return "I" + typeName;
            }

            return typeName;
        }

        // Fallback: prepend I
        if (!typeName.StartsWith('I'))
        {
            return "I" + typeName;
        }

        return typeName;
    }

    private static SyntaxToken[] ParseAccessibility(string accessibility)
    {
        return accessibility.ToUpperInvariant() switch
        {
            "PUBLIC" => [SyntaxFactory.Token(SyntaxKind.PublicKeyword)],
            "INTERNAL" => [SyntaxFactory.Token(SyntaxKind.InternalKeyword)],
            "PRIVATE" => [SyntaxFactory.Token(SyntaxKind.PrivateKeyword)],
            "PROTECTED" => [SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)],
            "PROTECTED INTERNAL" => [
                SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            ],
            "PRIVATE PROTECTED" => [
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            ],
            _ => [SyntaxFactory.Token(SyntaxKind.PublicKeyword)],
        };
    }

    private static ClassDeclarationSyntax CreateClassDeclaration(
        string typeName,
        SyntaxToken[] accessibilityTokens,
        BaseListSyntax? baseList)
    {
        var decl = SyntaxFactory.ClassDeclaration(typeName)
            .AddModifiers(accessibilityTokens);

        if (baseList is not null)
        {
            decl = decl.WithBaseList(baseList);
        }

        return decl;
    }

    private static InterfaceDeclarationSyntax CreateInterfaceDeclaration(
        string typeName,
        SyntaxToken[] accessibilityTokens,
        BaseListSyntax? baseList)
    {
        var decl = SyntaxFactory.InterfaceDeclaration(typeName)
            .AddModifiers(accessibilityTokens);

        if (baseList is not null)
        {
            decl = decl.WithBaseList(baseList);
        }

        return decl;
    }

    private static RecordDeclarationSyntax CreateRecordDeclaration(
        string typeName,
        SyntaxToken[] accessibilityTokens,
        BaseListSyntax? baseList)
    {
        var decl = SyntaxFactory.RecordDeclaration(SyntaxKind.RecordDeclaration,
                SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                SyntaxFactory.Identifier(typeName))
            .AddModifiers(accessibilityTokens)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        if (baseList is not null)
        {
            decl = decl.WithBaseList(baseList);
        }

        return decl;
    }

    private static StructDeclarationSyntax CreateStructDeclaration(
        string typeName,
        SyntaxToken[] accessibilityTokens,
        BaseListSyntax? baseList)
    {
        var decl = SyntaxFactory.StructDeclaration(typeName)
            .AddModifiers(accessibilityTokens);

        if (baseList is not null)
        {
            decl = decl.WithBaseList(baseList);
        }

        return decl;
    }

    private static RecordDeclarationSyntax CreateRecordStructDeclaration(
        string typeName,
        SyntaxToken[] accessibilityTokens,
        BaseListSyntax? baseList)
    {
        var decl = SyntaxFactory.RecordDeclaration(SyntaxKind.RecordStructDeclaration,
                SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                SyntaxFactory.Identifier(typeName))
            .AddModifiers(accessibilityTokens)
            .WithClassOrStructKeyword(SyntaxFactory.Token(SyntaxKind.StructKeyword))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        if (baseList is not null)
        {
            decl = decl.WithBaseList(baseList);
        }

        return decl;
    }
}
