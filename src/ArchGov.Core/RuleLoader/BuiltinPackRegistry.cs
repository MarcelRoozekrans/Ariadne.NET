using System.Reflection;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleLoader;

public sealed class BuiltinPackRegistry : IBuiltinPackRegistry
{
    private readonly IRulePackLoader _loader;
    private readonly Dictionary<string, string> _resourceMap;
    private readonly Dictionary<string, RulePack> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public BuiltinPackRegistry(IRulePackLoader loader)
    {
        _loader = loader;

        var assembly = typeof(BuiltinPackRegistry).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        _resourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            if (!resourceName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                || !resourceName.Contains("BuiltinPacks", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var packName = ExtractPackName(resourceName);
            _resourceMap[packName] = resourceName;
        }
    }

    public IReadOnlyList<string> GetAvailablePackNames()
    {
        return _resourceMap.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public RulePack GetPack(string name)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            if (!_resourceMap.TryGetValue(name, out var resourceName))
            {
                throw new KeyNotFoundException($"Built-in pack '{name}' not found. Available packs: {string.Join(", ", _resourceMap.Keys)}");
            }

            var assembly = typeof(BuiltinPackRegistry).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be loaded.");

            using var reader = new StreamReader(stream);
            var yaml = reader.ReadToEnd();
            var pack = _loader.LoadFromString(yaml);

            _cache[name] = pack;
            return pack;
        }
    }

    private static string ExtractPackName(string resourceName)
    {
        // Resource names are like: ArchGov.Core.BuiltinPacks.clean-architecture.yaml
        // We need to extract "clean-architecture"
        var lastDotYaml = resourceName.LastIndexOf(".yaml", StringComparison.OrdinalIgnoreCase);
        var withoutExtension = resourceName[..lastDotYaml];
        var lastDot = withoutExtension.LastIndexOf('.');
        return withoutExtension[(lastDot + 1)..];
    }
}
