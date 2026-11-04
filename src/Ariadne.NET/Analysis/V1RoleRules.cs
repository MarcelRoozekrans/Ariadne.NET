using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The v1 component-role checks, evaluated on the type graph's source types.</summary>
internal static class V1RoleRules
{
    /// <summary>Returns the type kind, accessibility and missing-interface violations, ordered by location and then rule id. Violations without a location come last, ordered by source type.</summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, V1LayerMap layers, RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);

        var matches = new List<(INamedTypeSymbol Type, ComponentRoleDefinition Role)>();
        var matchedRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in graph.SourceTypes)
        {
            // v1 roles apply to class, struct, interface and record declarations. Enums and delegates were never matched.
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
            {
                continue;
            }

            var layer = layers.LayerOf(type);
            if (layer is not null && MatchRole(type.Name, layer, pack.ComponentRoles) is { } role)
            {
                matches.Add((type, role));
                matchedRoleNames.Add(role.Name);
            }
        }

        var violations = new List<ArchitectureViolation>();
        foreach (ref readonly var match in CollectionsMarshal.AsSpan(matches))
        {
            var location = graph.SourceTypeLocations[match.Type];
            CheckTypeKind(match.Type, match.Role, in location, violations);
            CheckAccessibility(match.Type, match.Role, in location, violations);
        }

        foreach (var role in pack.ComponentRoles)
        {
            if (!role.RequiresInterface
                || string.IsNullOrEmpty(role.InterfaceRole)
                || matchedRoleNames.Contains(role.InterfaceRole))
            {
                continue;
            }

            foreach (ref readonly var match in CollectionsMarshal.AsSpan(matches))
            {
                if (ReferenceEquals(match.Role, role))
                {
                    violations.Add(new ArchitectureViolation
                    {
                        RuleId = "role/" + role.Name,
                        Kind = ViolationKind.MissingInterface,
                        SourceType = match.Type.ToDisplayString(),
                        Message = string.Create(
                            CultureInfo.InvariantCulture,
                            $"Type '{match.Type.Name}' matches role '{role.Name}' which requires interface role '{role.InterfaceRole}', but no matching interface was found"),
                    });
                }
            }
        }

        violations.Sort(CompareViolations);
        return violations;
    }

    private static ComponentRoleDefinition? MatchRole(string typeName, string layer, IList<ComponentRoleDefinition> roles)
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

            foreach (var alternative in role.AlternativeNames)
            {
                if (typeName.EndsWith(alternative, StringComparison.OrdinalIgnoreCase))
                {
                    return role;
                }
            }
        }

        return null;
    }

    private static void CheckTypeKind(
        INamedTypeSymbol type,
        ComponentRoleDefinition role,
        in SourceLocation location,
        List<ArchitectureViolation> violations)
    {
        var actual = ActualKind(type);
        if (actual == role.TypeKind)
        {
            return;
        }

        violations.Add(new ArchitectureViolation
        {
            RuleId = "role/" + role.Name,
            Kind = ViolationKind.TypeKindMismatch,
            SourceType = type.ToDisplayString(),
            Location = location,
            Message = string.Create(
                CultureInfo.InvariantCulture,
                $"Type '{type.Name}' matches role '{role.Name}' and should be a '{role.TypeKind}' but is declared as a '{actual}'"),
        });
    }

    private static void CheckAccessibility(
        INamedTypeSymbol type,
        ComponentRoleDefinition role,
        in SourceLocation location,
        List<ArchitectureViolation> violations)
    {
        // The default is public, so only a role that asks for internal can be violated.
        if (!string.Equals(role.Accessibility, "internal", StringComparison.OrdinalIgnoreCase)
            || type.DeclaredAccessibility == Accessibility.Internal)
        {
            return;
        }

        violations.Add(new ArchitectureViolation
        {
            RuleId = "role/" + role.Name,
            Kind = ViolationKind.AccessibilityMismatch,
            SourceType = type.ToDisplayString(),
            Location = location,
            Message = string.Create(
                CultureInfo.InvariantCulture,
                $"Type '{type.Name}' matches role '{role.Name}' and should be 'internal' but is declared '{Keyword(type.DeclaredAccessibility)}'"),
        });
    }

    private static CSharpTypeKind ActualKind(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Interface => CSharpTypeKind.Interface,
        TypeKind.Struct => type.IsRecord ? CSharpTypeKind.RecordStruct : CSharpTypeKind.Struct,
        _ => type.IsRecord ? CSharpTypeKind.Record : CSharpTypeKind.Class,
    };

    private static string Keyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Protected => "protected",
        _ => "private",
    };

    // Located violations come first, ordered by location, rule id, source type and kind.
    // Unlocated ones, the missing-interface violations, come last, ordered by source type, rule id and kind.
    private static int CompareViolations(ArchitectureViolation x, ArchitectureViolation y) =>
        (x.Location, y.Location) switch
        {
            ({ } a, { } b) => CompareLocated(x, a, y, b),
            ({ }, null) => -1,
            (null, { }) => 1,
            _ => CompareUnlocated(x, y),
        };

    private static int CompareLocated(ArchitectureViolation x, in SourceLocation a, ArchitectureViolation y, in SourceLocation b)
    {
        var result = TypeGraphBuilder.Compare(a, b);
        if (result == 0)
        {
            result = string.CompareOrdinal(x.RuleId, y.RuleId);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.SourceType, y.SourceType);
        }

        return result == 0 ? x.Kind.CompareTo(y.Kind) : result;
    }

    private static int CompareUnlocated(ArchitectureViolation x, ArchitectureViolation y)
    {
        var result = string.CompareOrdinal(x.SourceType, y.SourceType);
        if (result == 0)
        {
            result = string.CompareOrdinal(x.RuleId, y.RuleId);
        }

        return result == 0 ? x.Kind.CompareTo(y.Kind) : result;
    }
}
