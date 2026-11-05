using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Evaluates the <c>forbid</c> or <c>allowOnly</c> rules of a pack over the edges of a type graph.</summary>
internal sealed class DependencyRuleChecker
{
    private readonly List<Compiled> _rules;

    private DependencyRuleChecker(List<Compiled> rules) => _rules = rules;

    /// <summary>Compiles the matchers and exception selectors of the rules of one kind, so a run can reuse them for every project.</summary>
    public static DependencyRuleChecker Compile(LayerMap layers, RulePack pack, RuleKind kind)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);

        var rules = new List<Compiled>();
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind == kind)
            {
                rules.Add(Compile(rule, layers));
            }
        }

        return new DependencyRuleChecker(rules);
    }

    /// <summary>Returns one violation per rule, source type and target type, with the number of references and the location of the first.</summary>
    public IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var rules = _rules;
        if (rules.Count == 0)
        {
            return [];
        }

        var found = new List<Pair>();
        var index = new Dictionary<(int Rule, INamedTypeSymbol Source, INamedTypeSymbol Target), int>(KeyComparer.Instance);
        foreach (var edge in graph.Edges)
        {
            for (var i = 0; i < rules.Count; i++)
            {
                if (!rules[i].Violates(in edge))
                {
                    continue;
                }

                if (index.TryGetValue((i, edge.Source, edge.Target), out var at))
                {
                    found[at].Count++;
                }
                else
                {
                    index.Add((i, edge.Source, edge.Target), found.Count);
                    found.Add(new Pair(rules[i], edge));
                }
            }
        }

        var violations = new List<ArchitectureViolation>(found.Count);
        foreach (ref var pair in CollectionsMarshal.AsSpan(found))
        {
            violations.Add(ToViolation(pair));
        }

        return violations;
    }

    private static Compiled Compile(RuleDefinition rule, LayerMap layers)
    {
        var exceptions = new TypeSelector[rule.Exceptions.Count];
        for (var i = 0; i < exceptions.Length; i++)
        {
            var exception = rule.Exceptions[i];
            exceptions[i] = TypeSelector.From(exception.Namespaces, exception.Assemblies, exception.Types);
        }

        return new Compiled(rule, TargetMatcher.From(rule.From, layers), TargetMatcher.From(rule.To, layers), exceptions);
    }

    private static ArchitectureViolation ToViolation(Pair pair)
    {
        var rule = pair.Rule.Rule;
        var source = pair.First.Source.ToDisplayString();
        var target = pair.First.Target.ToDisplayString();
        var forbid = rule.Kind == RuleKind.Forbid;
        var message = forbid
            ? $"Rule '{rule.Id}' forbids this dependency: {source} references {target}"
            : $"Rule '{rule.Id}' allows only selected types to depend on {target}: {source} references {target}";

        return new ArchitectureViolation
        {
            RuleId = rule.Id,
            Kind = forbid ? ViolationKind.ForbiddenDependency : ViolationKind.DependencyNotPermitted,
            SourceType = source,
            Target = target,
            Location = pair.First.Location,
            Because = rule.Because,
            ReferenceCount = pair.Count,
            Message = message + (pair.Count > 1 ? string.Create(CultureInfo.InvariantCulture, $" {pair.Count} times") : string.Empty),
        };
    }

    private sealed class Compiled(RuleDefinition rule, TargetMatcher from, TargetMatcher to, TypeSelector[] exceptions)
    {
        public RuleDefinition Rule { get; } = rule;

        public bool Violates(in TypeEdge edge)
        {
            if (!to.Matches(edge.Target))
            {
                return false;
            }

            var applies = Rule.Kind == RuleKind.Forbid
                ? from.Matches(edge.Source)
                : !from.Matches(edge.Source) && !to.Matches(edge.Source);
            if (!applies)
            {
                return false;
            }

            foreach (var exception in exceptions)
            {
                if (exception.Matches(edge.Source))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class Pair(Compiled rule, TypeEdge first)
    {
        public Compiled Rule { get; } = rule;

        public TypeEdge First { get; } = first;

        public int Count { get; set; } = 1;
    }

    private sealed class KeyComparer : IEqualityComparer<(int Rule, INamedTypeSymbol Source, INamedTypeSymbol Target)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((int Rule, INamedTypeSymbol Source, INamedTypeSymbol Target) x, (int Rule, INamedTypeSymbol Source, INamedTypeSymbol Target) y) =>
            x.Rule == y.Rule
            && SymbolEqualityComparer.Default.Equals(x.Source, y.Source)
            && SymbolEqualityComparer.Default.Equals(x.Target, y.Target);

        public int GetHashCode((int Rule, INamedTypeSymbol Source, INamedTypeSymbol Target) obj) =>
            HashCode.Combine(obj.Rule, SymbolEqualityComparer.Default.GetHashCode(obj.Source), SymbolEqualityComparer.Default.GetHashCode(obj.Target));
    }
}
