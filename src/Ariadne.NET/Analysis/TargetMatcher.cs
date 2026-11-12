using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Tests whether a type is selected by a rule target: a list of layers, or a namespace, assembly and type selector, either
/// one narrowed by type-level traits. The answer is cached per symbol, so a type that many edges touch is matched once.
/// Like the layer map it uses, one instance serves one single-threaded check.
/// </summary>
internal sealed class TargetMatcher
{
    private readonly LayerMap _layerMap;
    private readonly HashSet<string> _layers;
    private readonly TypeSelector _selector;
    private readonly Dictionary<INamedTypeSymbol, bool> _cache = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, MemberName[]> _chains = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, bool> _containers = new(SymbolEqualityComparer.Default);

    private TargetMatcher(LayerMap layerMap, HashSet<string> layers, TypeSelector selector)
    {
        _layerMap = layerMap;
        _layers = layers;
        _selector = selector;
    }

    /// <summary>Whether the target holds <c>members</c>, so it selects member uses and never a type.</summary>
    public bool HasMembers => _selector.HasMembers;

    /// <summary>Compiles the target once. A target that selects nothing at all matches no type, so an empty from never fires a forbid rule and selects no source of an allowOnly rule.</summary>
    /// <param name="target">The target to compile.</param>
    /// <param name="layers">The layer map that resolves layer names.</param>
    /// <returns>The matcher.</returns>
    /// <exception cref="ArgumentException">The target mixes layers with namespaces, assemblies, types, base types, interfaces, attributes or members. Layers with traits are allowed.</exception>
    public static TargetMatcher From(TargetSelector target, LayerMap layers)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(layers);

        if (target.Layers.Count > 0 && (target.Namespaces.Count > 0 || target.Assemblies.Count > 0 || target.Types.Count > 0
                || target.BaseTypes.Count > 0 || target.Interfaces.Count > 0 || target.Attributes.Count > 0 || target.Members.Count > 0))
        {
            throw new ArgumentException("A target selects either layers or namespaces, assemblies, types, base types, interfaces, attributes and members, not both. Traits may join either.", nameof(target));
        }

        return new TargetMatcher(
            layers,
            new HashSet<string>(target.Layers, StringComparer.OrdinalIgnoreCase),
            TypeSelector.From(target));
    }

    /// <summary>
    /// Whether the type is selected. A target with layers selects a type in one of them that also matches the traits; a
    /// target without layers selects a type its selector matches, and nothing when the selector is empty. A target that
    /// holds <c>members</c> selects no type. A type covered by more than one layer is in none of them.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns>True when the type is selected.</returns>
    public bool Matches(INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!_cache.TryGetValue(type, out var matches))
        {
            // With layers, the selector holds traits at most, and an empty one matches every type.
            matches = !_selector.HasMembers
                && (_layers.Count > 0
                    ? _layerMap.LayerOf(type) is { } layer && _layers.Contains(layer) && _selector.Matches(type, _layerMap.Names)
                    : !_selector.IsEmpty && _selector.Matches(type, _layerMap.Names));
            _cache.Add(type, matches);
        }

        return matches;
    }

    /// <summary>
    /// Finds the name in the used member's chain that a <c>members</c> glob matches, provided the member's containing type
    /// satisfies the target's other keys. The chain is built once per member symbol.
    /// </summary>
    /// <param name="edge">The member use.</param>
    /// <param name="matched">The first matching member of the chain: the used member, or one it overrides or implements.</param>
    /// <returns>True when the use is selected.</returns>
    public bool TryMatchMember(in MemberEdge edge, out MemberName matched)
    {
        matched = default;
        if (!_selector.HasMembers || !ContainerMatches(edge.Member))
        {
            return false;
        }

        if (!_chains.TryGetValue(edge.Member, out var chain))
        {
            chain = MemberNames.NamedChain(edge.Member, _layerMap.Names);
            _chains.Add(edge.Member, chain);
        }

        foreach (var candidate in chain)
        {
            if (_selector.AnyMemberMatches(candidate.Name))
            {
                matched = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the used member is selected, as <see cref="TryMatchMember"/> decides.</summary>
    /// <param name="edge">The member use.</param>
    /// <returns>True when the use is selected.</returns>
    public bool MatchesMember(in MemberEdge edge) => TryMatchMember(in edge, out _);

    // The other keys test the used member's containing type, cached per type.
    private bool ContainerMatches(ISymbol member)
    {
        if (_selector.IsEmpty)
        {
            return true;
        }

        var type = MemberNames.DeclaringTypeOf(member);
        if (!_containers.TryGetValue(type, out var matches))
        {
            matches = _selector.Matches(type, _layerMap.Names);
            _containers.Add(type, matches);
        }

        return matches;
    }
}
