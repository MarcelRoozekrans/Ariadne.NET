using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Analysis;

/// <summary>
/// Finds the layers of a pack that select an assembly and namespace, for code that has no compilation yet, such as a file
/// that is about to be written. It follows <c>LayerMap</c>: a layer's namespace and assembly lists are AND-ed, the entries
/// of a list are OR-ed and a layer with neither list selects nothing.
/// </summary>
public static class LayerProbe
{
    /// <summary>The names of all layers of <paramref name="resolvedPack"/> that select the assembly and namespace, in pack order.</summary>
    /// <param name="resolvedPack">The resolved pack whose layers to probe.</param>
    /// <param name="assemblyName">The assembly name.</param>
    /// <param name="namespaceName">The namespace, empty for the global namespace.</param>
    /// <returns>The layer names, empty when no layer selects them.</returns>
    public static IReadOnlyList<string> LayersOf(RulePack resolvedPack, string assemblyName, string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(namespaceName);

        var matches = new List<string>();
        foreach (var layer in resolvedPack.Layers)
        {
            if (layer.Namespaces.Count == 0 && layer.Assemblies.Count == 0)
            {
                continue;
            }

            if (AnyMatches(layer.Namespaces, namespaceName, dotted: true) && AnyMatches(layer.Assemblies, assemblyName, dotted: false))
            {
                matches.Add(layer.Name);
            }
        }

        return matches;
    }

    // An empty list does not constrain. The loader validates patterns, so a malformed one never matches here.
    private static bool AnyMatches(IList<string> patterns, string name, bool dotted)
    {
        if (patterns.Count == 0)
        {
            return true;
        }

        foreach (var pattern in patterns)
        {
            var glob = dotted ? Glob.Dotted(pattern) : Glob.Flat(pattern);
            if (glob.IsSuccess && glob.Value.IsMatch(name))
            {
                return true;
            }
        }

        return false;
    }
}
