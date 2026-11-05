using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Tests whether a type is selected by a rule target: a list of layers, or a namespace, assembly and type selector.</summary>
internal sealed class TargetMatcher
{
    private readonly LayerMap _layerMap;
    private readonly HashSet<string> _layers;
    private readonly TypeSelector _selector;

    private TargetMatcher(LayerMap layerMap, HashSet<string> layers, TypeSelector selector)
    {
        _layerMap = layerMap;
        _layers = layers;
        _selector = selector;
    }

    /// <summary>Compiles the target once. A target that selects nothing at all matches no type, so an empty from never fires a forbid rule and selects no source of an allowOnly rule.</summary>
    /// <param name="target">The target to compile.</param>
    /// <param name="layers">The layer map that resolves layer names.</param>
    /// <returns>The matcher.</returns>
    /// <exception cref="ArgumentException">The target mixes layers with namespaces, assemblies or types.</exception>
    public static TargetMatcher From(TargetSelector target, LayerMap layers)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layers);

        if (target.Layers.Count > 0 && (target.Namespaces.Count > 0 || target.Assemblies.Count > 0 || target.Types.Count > 0))
        {
            throw new ArgumentException("A target selects either layers or namespaces, assemblies and types, not both.", nameof(target));
        }

        return new TargetMatcher(
            layers,
            new HashSet<string>(target.Layers, StringComparer.OrdinalIgnoreCase),
            TypeSelector.From(target.Namespaces, target.Assemblies, target.Types));
    }

    /// <summary>Whether the type is selected. A type covered by more than one layer is in none of them.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the type is selected.</returns>
    public bool Matches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_layers.Count > 0 && _layerMap.LayerOf(type) is { } layer && _layers.Contains(layer))
        {
            return true;
        }

        return !_selector.IsEmpty && _selector.Matches(type);
    }
}
