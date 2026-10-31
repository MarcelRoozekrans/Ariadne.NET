using System.ComponentModel;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using ModelContextProtocol.Server;

namespace Ariadne.NET.Mcp.Resources;

[McpServerResourceType]
public static class PackResources
{
    [McpServerResource(UriTemplate = "ariadne://packs/{name}")]
    [Description("Get the full YAML content of a built-in architecture rule pack.")]
    public static string GetPackContent(
        IBuiltinPackRegistry registry,
        [Description("Pack name (e.g. clean-architecture, hexagonal-architecture, cqrs)")]
        string name)
    {
        // Validate pack exists (throws KeyNotFoundException if not found)
        registry.GetPack(name);

        // Read raw YAML from embedded resource
        var assembly = typeof(BuiltinPackRegistry).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r =>
                r.EndsWith($".{name}.yaml", StringComparison.OrdinalIgnoreCase)
                && r.Contains("BuiltinPacks", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource for pack '{name}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
