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

    /// <summary>
    /// Returns one violation per rule, source type and target, with the number of references and the location of the first. A
    /// rule whose <c>to</c> holds <c>members</c> is evaluated on the member edges and a rule without on the type edges.
    /// </summary>
    public IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var rules = _rules;
        if (rules.Count == 0)
        {
            return [];
        }

        var found = new List<Pair>();
        var index = new Dictionary<(int Rule, INamedTypeSymbol Source, ISymbol Target), int>(KeyComparer.Instance);
        foreach (var edge in graph.Edges)
        {
            for (var i = 0; i < rules.Count; i++)
            {
                if (!rules[i].ToMembers && rules[i].Violates(in edge))
                {
                    Record(found, index, i, edge.Source, edge.Target, edge.Location, default);
                }
            }
        }

        foreach (var edge in graph.MemberEdges)
        {
            for (var i = 0; i < rules.Count; i++)
            {
                if (rules[i].ToMembers && rules[i].Violates(in edge, out var matched))
                {
                    Record(found, index, i, edge.Source, edge.Member, edge.Location, matched);
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

    private void Record(List<Pair> found, Dictionary<(int Rule, INamedTypeSymbol Source, ISymbol Target), int> index, int rule, INamedTypeSymbol source, ISymbol target, in SourceLocation location, in MemberName matched)
    {
        if (index.TryGetValue((rule, source, target), out var at))
        {
            found[at].Count++;
        }
        else
        {
            index.Add((rule, source, target), found.Count);
            found.Add(new Pair(_rules[rule], source, target, location, matched));
        }
    }

    private static Compiled Compile(RuleDefinition rule, LayerMap layers)
    {
        var exceptions = new TypeSelector[rule.Exceptions.Count];
        for (var i = 0; i < exceptions.Length; i++)
        {
            exceptions[i] = TypeSelector.From(rule.Exceptions[i]);
        }

        return new Compiled(rule, TargetMatcher.From(rule.From, layers), TargetMatcher.From(rule.To, layers), exceptions, layers.Names);
    }

    private static ArchitectureViolation ToViolation(Pair pair)
    {
        var rule = pair.Rule.Rule;
        var source = pair.Source.ToDisplayString();
        var target = pair.Target is INamedTypeSymbol type ? type.ToDisplayString() : pair.Rule.DisplayNameOf(pair.Target);
        var matched = pair.Matched;
        var forbid = rule.Kind == RuleKind.Forbid;
        var verb = pair.Target is INamedTypeSymbol ? "references" : pair.Target is IMethodSymbol ? "calls" : "uses";
        var message = forbid
            ? $"Rule '{rule.Id}' forbids this dependency: {source} {verb} {target}"
            : $"Rule '{rule.Id}' allows only selected types to depend on {target}: {source} {verb} {target}";

        return new ArchitectureViolation
        {
            RuleId = rule.Id,
            Kind = forbid ? ViolationKind.ForbiddenDependency : ViolationKind.DependencyNotPermitted,
            SourceType = source,
            Target = target,
            Location = pair.Location,
            Because = rule.Because,
            ReferenceCount = pair.Count,
            Message = message + Via(in matched, target) + (pair.Count > 1 ? string.Create(CultureInfo.InvariantCulture, $" {pair.Count} times") : string.Empty),
        };
    }

    // The member of the pattern, when it is not the used one: " (which overrides X)" or " (which implements X)".
    private static string Via(in MemberName matched, string target)
    {
        if (matched.Member is null || string.Equals(matched.Name, target, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var verb = matched.Member.ContainingType.TypeKind == TypeKind.Interface ? "implements" : "overrides";
        return $" (which {verb} {matched.Name})";
    }

    private sealed class Compiled(RuleDefinition rule, TargetMatcher from, TargetMatcher to, TypeSelector[] exceptions, TypeNames names)
    {
        // Whether an exception covers a source type, cached per symbol: a violating type is usually seen on many edges.
        private readonly Dictionary<INamedTypeSymbol, bool> _excepted = new(SymbolEqualityComparer.Default);

        public RuleDefinition Rule { get; } = rule;

        public string DisplayNameOf(ISymbol member) => MemberNames.DisplayNameOf(member, names);

        /// <summary>Whether the rule selects member uses instead of types.</summary>
        public bool ToMembers { get; } = to.HasMembers;

        public bool Violates(in TypeEdge edge) =>
            to.Matches(edge.Target)
            && (Rule.Kind == RuleKind.Forbid
                ? from.Matches(edge.Source)
                : !from.Matches(edge.Source) && !to.Matches(edge.Source))
            && !IsExcepted(edge.Source);

        // Under allowOnly a type may use its own members, and so may a type nested in it: that is its own implementation. A derived type may not.
        private static bool IsWithin(INamedTypeSymbol source, INamedTypeSymbol declaring)
        {
            for (INamedTypeSymbol? current = source.OriginalDefinition; current is not null; current = current.ContainingType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, declaring))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Violates(in MemberEdge edge, out MemberName matched) =>
            to.TryMatchMember(edge, out matched)
            && (Rule.Kind == RuleKind.Forbid
                ? from.Matches(edge.Source)
                : !from.Matches(edge.Source) && !IsWithin(edge.Source, MemberNames.DeclaringTypeOf(edge.Member)))
            && !IsExcepted(edge.Source);

        private bool IsExcepted(INamedTypeSymbol source)
        {
            if (exceptions.Length == 0)
            {
                return false;
            }

            if (!_excepted.TryGetValue(source, out var excepted))
            {
                excepted = false;
                foreach (var exception in exceptions)
                {
                    if (exception.Matches(source, names))
                    {
                        excepted = true;
                        break;
                    }
                }

                _excepted.Add(source, excepted);
            }

            return excepted;
        }
    }

    private sealed class Pair(Compiled rule, INamedTypeSymbol source, ISymbol target, in SourceLocation location, in MemberName matched)
    {
        public Compiled Rule { get; } = rule;

        public INamedTypeSymbol Source { get; } = source;

        public ISymbol Target { get; } = target;

        public SourceLocation Location { get; } = location;

        public MemberName Matched { get; } = matched;

        public int Count { get; set; } = 1;
    }

    private sealed class KeyComparer : IEqualityComparer<(int Rule, INamedTypeSymbol Source, ISymbol Target)>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals((int Rule, INamedTypeSymbol Source, ISymbol Target) x, (int Rule, INamedTypeSymbol Source, ISymbol Target) y) =>
            x.Rule == y.Rule
            && SymbolEqualityComparer.Default.Equals(x.Source, y.Source)
            && SymbolEqualityComparer.Default.Equals(x.Target, y.Target);

        public int GetHashCode((int Rule, INamedTypeSymbol Source, ISymbol Target) obj) =>
            HashCode.Combine(obj.Rule, SymbolEqualityComparer.Default.GetHashCode(obj.Source), SymbolEqualityComparer.Default.GetHashCode(obj.Target));
    }
}
