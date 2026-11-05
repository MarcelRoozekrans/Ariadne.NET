using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The v1 layer-dependency rule, evaluated on the type graph.</summary>
internal static class V1LayerRules
{
    /// <summary>Returns one violation per source and target type pair whose layers are different and not allowed, ordered by first location and then rule id.</summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, V1LayerMap layers, RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);

        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in pack.Layers)
        {
            allowed[layer.Name] = new HashSet<string>(layer.AllowedDependencies, StringComparer.OrdinalIgnoreCase);
        }

        var pairs = new Dictionary<(INamedTypeSymbol Source, INamedTypeSymbol Target), int>(PairComparer.Instance);
        var found = new List<Pair>();
        foreach (var edge in graph.Edges)
        {
            var sourceLayer = layers.LayerOf(edge.Source);
            var targetLayer = layers.LayerOf(edge.Target);
            if (sourceLayer is null
                || targetLayer is null
                || string.Equals(sourceLayer, targetLayer, StringComparison.OrdinalIgnoreCase)
                || (allowed.TryGetValue(sourceLayer, out var set) && set.Contains(targetLayer)))
            {
                continue;
            }

            if (pairs.TryGetValue((edge.Source, edge.Target), out var index))
            {
                found[index].Count++;
            }
            else
            {
                pairs.Add((edge.Source, edge.Target), found.Count);
                found.Add(new Pair(edge, sourceLayer, targetLayer, 1));
            }
        }

        var violations = new List<ArchitectureViolation>(found.Count);
        foreach (ref var pair in CollectionsMarshal.AsSpan(found))
        {
            violations.Add(ToViolation(pair));
        }

        violations.Sort(CompareViolations);
        return violations;
    }

    private static ArchitectureViolation ToViolation(Pair pair)
    {
        var source = pair.First.Source.ToDisplayString();
        var target = pair.First.Target.ToDisplayString();
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"Layer '{pair.SourceLayer}' is not allowed to depend on '{pair.TargetLayer}': {source} references {target}");

        return new ArchitectureViolation
        {
            RuleId = "layer/" + pair.SourceLayer,
            Kind = ViolationKind.DependencyNotAllowed,
            SourceType = source,
            Target = target,
            Location = pair.First.Location,
            ReferenceCount = pair.Count,
            Message = message + CountSuffix(pair.Count),
        };
    }

    /// <summary>
    /// Returns <paramref name="violation"/> with <paramref name="count"/> as its reference count. A dependency violation's message
    /// states the count, so it is rebuilt to match; other kinds keep their message.
    /// </summary>
    public static ArchitectureViolation WithReferenceCount(ArchitectureViolation violation, int count)
    {
        ArgumentNullException.ThrowIfNull(violation);

        if (violation.Kind != ViolationKind.DependencyNotAllowed)
        {
            return violation with { ReferenceCount = count };
        }

        var suffix = CountSuffix(violation.ReferenceCount);
        var message = violation.Message.EndsWith(suffix, StringComparison.Ordinal)
            ? violation.Message[..^suffix.Length]
            : violation.Message;
        return violation with { ReferenceCount = count, Message = message + CountSuffix(count) };
    }

    // The message names the count only when the source type references the target more than once.
    private static string CountSuffix(int count) =>
        count > 1 ? string.Create(CultureInfo.InvariantCulture, $" {count} times") : string.Empty;

    private static int CompareViolations(ArchitectureViolation x, ArchitectureViolation y)
    {
        // Every violation is built from an edge, and edges always carry a location.
        var a = x.Location!.Value;
        var b = y.Location!.Value;
        var result = string.CompareOrdinal(a.FilePath, b.FilePath);
        if (result == 0)
        {
            result = a.Line.CompareTo(b.Line);
        }

        if (result == 0)
        {
            result = a.Column.CompareTo(b.Column);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.RuleId, y.RuleId);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.SourceType, y.SourceType);
        }

        return result == 0 ? string.CompareOrdinal(x.Target, y.Target) : result;
    }

    private sealed class Pair(TypeEdge first, string sourceLayer, string targetLayer, int count)
    {
        public TypeEdge First { get; } = first;

        public string SourceLayer { get; } = sourceLayer;

        public string TargetLayer { get; } = targetLayer;

        public int Count { get; set; } = count;
    }

    private sealed class PairComparer : IEqualityComparer<(INamedTypeSymbol Source, INamedTypeSymbol Target)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((INamedTypeSymbol Source, INamedTypeSymbol Target) x, (INamedTypeSymbol Source, INamedTypeSymbol Target) y) =>
            SymbolEqualityComparer.Default.Equals(x.Source, y.Source) && SymbolEqualityComparer.Default.Equals(x.Target, y.Target);

        public int GetHashCode((INamedTypeSymbol Source, INamedTypeSymbol Target) obj) =>
            HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(obj.Source), SymbolEqualityComparer.Default.GetHashCode(obj.Target));
    }
}
