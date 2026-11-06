using System.Globalization;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The component-role checks, evaluated on the type graph's source types.</summary>
internal static class RoleRules
{
    /// <summary>Returns the type kind, accessibility and missing-interface violations, ordered by location and then rule id. Violations without a location come last, ordered by source type.</summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, LayerMap layers, RulePack pack)
    {
        var matches = Match(graph, layers, pack);
        var violations = new List<ArchitectureViolation>(CheckShape(matches));
        violations.AddRange(CheckMissingInterface(matches, [matches], pack));
        violations.Sort(CompareViolations);
        return violations;
    }

    /// <summary>Finds the source types of <paramref name="graph"/> that match a component role.</summary>
    public static RoleMatches Match(TypeGraph graph, LayerMap layers, RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);

        var matches = new List<(INamedTypeSymbol Type, ComponentRoleDefinition Role)>();
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
            }
        }

        return new RoleMatches(graph, matches);
    }

    /// <summary>Returns the type kind and accessibility violations of the matched types, in match order.</summary>
    public static IReadOnlyList<ArchitectureViolation> CheckShape(RoleMatches matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var violations = new List<ArchitectureViolation>();
        foreach (var (type, role) in matches.Items)
        {
            var location = matches.Graph.SourceTypeLocations[type];
            CheckTypeKind(type, role, in location, violations);
            CheckAccessibility(type, role, in location, violations);
        }

        return violations;
    }

    /// <summary>
    /// Returns the missing-interface violations of the types in <paramref name="own"/>. An interface role counts as present
    /// when a type in any of the graphs in <paramref name="scope"/> matches it, so the graphs can be those of several projects.
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> CheckMissingInterface(
        RoleMatches own,
        IReadOnlyCollection<RoleMatches> scope,
        RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(pack);

        var matchedRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var matches in scope)
        {
            foreach (var (_, role) in matches.Items)
            {
                matchedRoleNames.Add(role.Name);
            }
        }

        var violations = new List<ArchitectureViolation>();
        foreach (var role in pack.ComponentRoles)
        {
            if (!role.RequiresInterface
                || string.IsNullOrEmpty(role.InterfaceRole)
                || matchedRoleNames.Contains(role.InterfaceRole))
            {
                continue;
            }

            foreach (var match in own.Items)
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

    internal static CSharpTypeKind ActualKind(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Interface => CSharpTypeKind.Interface,
        TypeKind.Struct => type.IsRecord ? CSharpTypeKind.RecordStruct : CSharpTypeKind.Struct,
        _ => type.IsRecord ? CSharpTypeKind.Record : CSharpTypeKind.Class,
    };

    internal static string Keyword(Accessibility accessibility) => accessibility switch
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
