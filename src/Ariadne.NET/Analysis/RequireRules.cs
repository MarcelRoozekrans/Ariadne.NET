using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The <c>require</c> rules of a pack, evaluated on the type graph's source types.</summary>
internal static class RequireRules
{
    /// <summary>
    /// Returns one violation per rule, selected source type and failed requirement, located at the type. Placement is explicit:
    /// a type the rule selects that no layer covers, or that another layer covers, breaks <c>layer</c>. A type that several
    /// layers cover is skipped for <c>layer</c>, because it is already a layer overlap. Enums and delegates are subjects of
    /// <c>layer</c> only. An accessibility other than <c>internal</c> requires nothing.
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, LayerMap layers, RulePack pack) =>
        Compile(layers, pack).Check(graph);

    /// <summary>Compiles the rules once, for a run that checks several graphs.</summary>
    public static Checker Compile(LayerMap layers, RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);

        var rules = new List<Compiled>();
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind == RuleKind.Require && rule.Require is { } requirement)
            {
                rules.Add(Compile(rule, requirement, layers));
            }
        }

        return new Checker(rules, layers);
    }

    /// <summary>The compiled <c>require</c> rules of a pack.</summary>
    internal sealed class Checker(List<Compiled> rules, LayerMap layers)
    {
        public IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph)
        {
            ArgumentNullException.ThrowIfNull(graph);

            if (rules.Count == 0)
            {
                return [];
            }

            var violations = new List<ArchitectureViolation>();
            foreach (var type in graph.SourceTypes)
            {
                var location = graph.SourceTypeLocations[type];
                foreach (ref readonly var rule in CollectionsMarshal.AsSpan(rules))
                {
                    if (rule.From.Matches(type) && !rule.IsExcepted(type, layers.Names))
                    {
                        rule.Check(type, in location, layers, violations);
                    }
                }
            }

            return violations;
        }
    }

    private static Compiled Compile(RuleDefinition rule, RuleRequirement requirement, LayerMap layers)
    {
        var exceptions = new TypeSelector[rule.Exceptions.Count];
        for (var i = 0; i < exceptions.Length; i++)
        {
            var exception = rule.Exceptions[i];
            exceptions[i] = TypeSelector.From(exception.Namespaces, exception.Assemblies, exception.Types);
        }

        TypeSelector? implements = null;
        if (requirement.Implements is { } selector)
        {
            var compiled = TypeSelector.From(selector.Namespaces, selector.Assemblies, selector.Types);
            implements = compiled.IsEmpty ? null : compiled;
        }

        return new Compiled(rule, requirement, TargetMatcher.From(rule.From, layers), exceptions, implements);
    }

    internal sealed class Compiled(RuleDefinition rule, RuleRequirement requirement, TargetMatcher from, TypeSelector[] exceptions, TypeSelector? implements)
    {
        public TargetMatcher From { get; } = from;

        public bool IsExcepted(INamedTypeSymbol type, TypeNames names)
        {
            foreach (var exception in exceptions)
            {
                if (exception.Matches(type, names))
                {
                    return true;
                }
            }

            return false;
        }

        public void Check(INamedTypeSymbol type, in SourceLocation location, LayerMap layers, List<ArchitectureViolation> violations)
        {
            if (requirement.Layer is { } layer)
            {
                CheckLayer(type, layer, in location, layers, violations);
            }

            // Enums and delegates were never subjects of the shape checks.
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
            {
                return;
            }

            if (requirement.Kind is { } kind && RoleRules.ActualKind(type) is var actual && actual != kind)
            {
                Add(type, in location, violations, ViolationKind.TypeKindMismatch, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Type '{type.Name}' is selected by rule '{rule.Id}' and should be a '{kind}' but is declared as a '{actual}'"));
            }

            if (string.Equals(requirement.Accessibility, "internal", StringComparison.OrdinalIgnoreCase)
                && type.DeclaredAccessibility != Accessibility.Internal)
            {
                Add(type, in location, violations, ViolationKind.AccessibilityMismatch, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Type '{type.Name}' is selected by rule '{rule.Id}' and should be 'internal' but is declared '{RoleRules.Keyword(type.DeclaredAccessibility)}'"));
            }

            if (implements is not null && !ImplementsAny(type, layers.Names))
            {
                Add(type, in location, violations, ViolationKind.MissingInterface, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Type '{type.Name}' is selected by rule '{rule.Id}' and should implement an interface selected by the rule but does not"));
            }
        }

        private void CheckLayer(INamedTypeSymbol type, string layer, in SourceLocation location, LayerMap layers, List<ArchitectureViolation> violations)
        {
            var covering = layers.LayersOf(type);
            if (covering.Count > 1 || (covering.Count == 1 && string.Equals(covering[0], layer, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var actual = covering.Count == 0 ? "in no layer" : string.Create(CultureInfo.InvariantCulture, $"in layer '{covering[0]}'");
            Add(type, in location, violations, ViolationKind.WrongLayer, string.Create(
                CultureInfo.InvariantCulture,
                $"Type '{type.Name}' is selected by rule '{rule.Id}' and should be in layer '{layer}' but is {actual}"));
        }

        // Every interface counts, including those the type gets through a base class.
        private bool ImplementsAny(INamedTypeSymbol type, TypeNames names)
        {
            foreach (var implemented in type.AllInterfaces)
            {
                if (implements!.Matches(implemented.OriginalDefinition, names))
                {
                    return true;
                }
            }

            return false;
        }

        private void Add(INamedTypeSymbol type, in SourceLocation location, List<ArchitectureViolation> violations, ViolationKind kind, string message) =>
            violations.Add(new ArchitectureViolation
            {
                RuleId = rule.Id,
                Kind = kind,
                SourceType = type.ToDisplayString(),
                Location = location,
                Because = rule.Because,
                Message = message,
            });
    }
}
