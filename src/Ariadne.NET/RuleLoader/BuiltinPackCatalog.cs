using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// The state behind one <see cref="BuiltinPackRegistry"/>: the embedded pack resources, the loader that parses them and
/// the packs loaded so far. <c>PackOperations.GetPack</c> reads and fills it under <see cref="Lock"/>.
/// </summary>
internal sealed class BuiltinPackCatalog
{
    private static readonly Dictionary<string, string> ResourceMap = MapResources();

    public BuiltinPackCatalog(IRulePackLoader loader)
    {
        Loader = loader;
    }

    /// <summary>Gets the loader that parses an embedded pack.</summary>
    public IRulePackLoader Loader { get; }

    /// <summary>Gets the packs loaded so far, by name; read and written only under <see cref="Lock"/>.</summary>
    public Dictionary<string, RulePack> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the lock that guards <see cref="Cache"/>.</summary>
    public Lock Lock { get; } = new();

    /// <summary>Gets the names of the embedded packs, sorted.</summary>
    public static IReadOnlyList<string> Names => ResourceMap.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Finds the manifest resource that holds the embedded pack <paramref name="name"/>.</summary>
    public static bool TryGetResource(string name, out string resourceName) =>
        ResourceMap.TryGetValue(name, out resourceName!);

    private static Dictionary<string, string> MapResources()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in typeof(BuiltinPackCatalog).Assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                || !resourceName.Contains("BuiltinPacks", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            map[ExtractPackName(resourceName)] = resourceName;
        }

        return map;
    }

    private static string ExtractPackName(string resourceName)
    {
        // Resource names are like: Ariadne.NET.BuiltinPacks.clean-architecture.yaml
        // We need to extract "clean-architecture"
        var lastDotYaml = resourceName.LastIndexOf(".yaml", StringComparison.OrdinalIgnoreCase);
        var withoutExtension = resourceName[..lastDotYaml];
        var lastDot = withoutExtension.LastIndexOf('.');
        return withoutExtension[(lastDot + 1)..];
    }
}
