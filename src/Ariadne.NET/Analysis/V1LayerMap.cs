using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Maps types to the v1 layers of a rule pack by the segments of their namespace. One instance serves one single-threaded check.</summary>
internal sealed class V1LayerMap
{
    private readonly Dictionary<string, string> _layerNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ISymbol, string?> _cache = new(SymbolEqualityComparer.Default);

    public V1LayerMap(RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        foreach (var layer in pack.Layers)
        {
            _layerNames.TryAdd(layer.Name, layer.Name);
        }
    }

    /// <summary>The pack's spelling of the layer of <paramref name="type"/>, or null when no namespace segment names a layer.</summary>
    public string? LayerOf(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_cache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var layer = FindLayer(type.ContainingNamespace);
        _cache[type] = layer;
        return layer;
    }

    // Walks the namespace chain from the innermost segment outwards, so the last matching segment wins.
    private string? FindLayer(INamespaceSymbol? ns)
    {
        for (; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
        {
            if (_layerNames.TryGetValue(ns.Name, out var layer))
            {
                return layer;
            }
        }

        return null;
    }
}
