using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The layer-dependency and layer-overlap rules, evaluated on the type graph.</summary>
internal static class LayerRules
{
    private const string OverlapBecause = "A type must belong to at most one layer.";

    /// <summary>
    /// Returns one violation per source and target type pair whose layers are different and not allowed, and one overlap violation
    /// per type that more than one layer selects. A type in several layers takes part in no dependency check. Ordered by first
    /// location and then rule id, with unlocated violations last.
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, LayerMap layers, RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);

        var byName = new Dictionary<string, LayerDefinition>(StringComparer.OrdinalIgnoreCase);
        var allowed = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in pack.Layers)
        {
            byName.TryAdd(layer.Name, layer);
            allowed[layer.Name] = new HashSet<string>(layer.AllowedDependencies, StringComparer.OrdinalIgnoreCase);
        }

        var violations = new List<ArchitectureViolation>();
        var overlapped = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AddSourceOverlaps(graph, layers, byName, overlapped, violations);

        var pairs = new Dictionary<(INamedTypeSymbol Source, INamedTypeSymbol Target), int>(PairComparer.Instance);
        var found = new List<Pair>();
        foreach (var edge in graph.Edges)
        {
            var sourceLayers = layers.LayersOf(edge.Source);
            var targetLayers = layers.LayersOf(edge.Target);
            if (targetLayers.Count > 1 && overlapped.Add(edge.Target))
            {
                // A metadata type is no source type, so it has no declaration to point at.
                violations.Add(ToOverlap(edge.Target, targetLayers, byName));
            }

            if (sourceLayers.Count != 1
                || targetLayers.Count != 1
                || string.Equals(sourceLayers[0], targetLayers[0], StringComparison.OrdinalIgnoreCase)
                || (allowed.TryGetValue(sourceLayers[0], out var set) && set.Contains(targetLayers[0])))
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
                found.Add(new Pair(edge, byName[sourceLayers[0]], targetLayers[0], 1));
            }
        }

        foreach (ref var pair in CollectionsMarshal.AsSpan(found))
        {
            violations.Add(ToViolation(pair));
        }

        violations.Sort(CompareViolations);
        return violations;
    }

    private static void AddSourceOverlaps(
        TypeGraph graph,
        LayerMap layers,
        Dictionary<string, LayerDefinition> byName,
        HashSet<INamedTypeSymbol> overlapped,
        List<ArchitectureViolation> violations)
    {
        foreach (var type in graph.SourceTypes)
        {
            var covering = layers.LayersOf(type);
            if (covering.Count > 1 && overlapped.Add(type))
            {
                violations.Add(ToOverlap(type, covering, byName) with { Location = graph.SourceTypeLocations[type] });
            }
        }
    }

    private static ArchitectureViolation ToOverlap(
        INamedTypeSymbol type,
        IReadOnlyList<string> covering,
        Dictionary<string, LayerDefinition> byName)
    {
        var ids = new string[covering.Count];
        var names = new string[covering.Count];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = byName[covering[i]].Id;
            names[i] = "'" + covering[i] + "'";
        }

        var source = type.ToDisplayString();
        return new ArchitectureViolation
        {
            RuleId = string.Join('+', ids),
            Kind = ViolationKind.LayerOverlap,
            SourceType = source,
            Because = OverlapBecause,
            Message = string.Create(CultureInfo.InvariantCulture, $"Type '{source}' is covered by layers {string.Join(", ", names)}"),
        };
    }

    private static ArchitectureViolation ToViolation(Pair pair)
    {
        var source = pair.First.Source.ToDisplayString();
        var target = pair.First.Target.ToDisplayString();
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"Layer '{pair.SourceLayer.Name}' is not allowed to depend on '{pair.TargetLayer}': {source} references {target}");

        return new ArchitectureViolation
        {
            RuleId = pair.SourceLayer.Id,
            Kind = ViolationKind.DependencyNotAllowed,
            SourceType = source,
            Target = target,
            Location = pair.First.Location,
            Because = pair.SourceLayer.Because,
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

    // Located violations first, then the unlocated ones.
    private static int CompareViolations(ArchitectureViolation x, ArchitectureViolation y)
    {
        int result;
        switch (x.Location, y.Location)
        {
            case ({ } a, { } b):
                result = string.CompareOrdinal(a.FilePath, b.FilePath);
                if (result == 0)
                {
                    result = a.Line.CompareTo(b.Line);
                }

                if (result == 0)
                {
                    result = a.Column.CompareTo(b.Column);
                }

                break;
            case ({ }, null):
                return -1;
            case (null, { }):
                return 1;
            default:
                result = 0;
                break;
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.RuleId, y.RuleId);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.SourceType, y.SourceType);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.Target, y.Target);
        }

        return result == 0 ? x.Kind.CompareTo(y.Kind) : result;
    }

    private sealed class Pair(TypeEdge first, LayerDefinition sourceLayer, string targetLayer, int count)
    {
        public TypeEdge First { get; } = first;

        public LayerDefinition SourceLayer { get; } = sourceLayer;

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
