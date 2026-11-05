using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Maps types to the layers of a rule pack by the layers' namespace and assembly selectors. One instance serves one
/// single-threaded check, which may span several compilations: a symbol of one compilation never equals one of another,
/// and equal symbols have the same names, so a cached layer is right for every project of the run.
/// </summary>
internal sealed class LayerMap
{
    private static readonly IReadOnlyList<string> NoLayers = [];

    private readonly (string Name, TypeSelector Selector)[] _layers;
    private readonly Dictionary<ISymbol, IReadOnlyList<string>> _cache = new(SymbolEqualityComparer.Default);

    /// <summary>The type names of the check, shared with the rule selectors that use this map.</summary>
    public TypeNames Names { get; } = new();

    /// <summary>Compiles the selector of every layer once. A layer without namespaces and assemblies selects nothing.</summary>
    /// <param name="pack">The pack whose layers to map to.</param>
    public LayerMap(RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var layers = new List<(string, TypeSelector)>(pack.Layers.Count);
        foreach (var layer in pack.Layers)
        {
            var selector = TypeSelector.From(layer.Namespaces, layer.Assemblies, []);
            if (!selector.IsEmpty)
            {
                layers.Add((layer.Name, selector));
            }
        }

        _layers = [.. layers];
    }

    /// <summary>The name of the one layer of <paramref name="type"/>, or null when it is in no layer or in more than one.</summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The layer name, or null.</returns>
    public string? LayerOf(INamedTypeSymbol type)
    {
        var layers = LayersOf(type);
        return layers.Count == 1 ? layers[0] : null;
    }

    /// <summary>The names of all layers that select <paramref name="type"/>, in pack order.</summary>
    /// <param name="type">The type to look up.</param>
    /// <returns>The layer names, empty when none match.</returns>
    public IReadOnlyList<string> LayersOf(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_cache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        List<string>? matches = null;
        foreach (var (name, selector) in _layers)
        {
            if (selector.Matches(type, Names))
            {
                (matches ??= []).Add(name);
            }
        }

        IReadOnlyList<string> result = matches is null ? NoLayers : matches.ToArray();
        _cache[type] = result;
        return result;
    }
}
