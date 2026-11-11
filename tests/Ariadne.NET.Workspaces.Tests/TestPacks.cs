using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Rule packs for the fixture solutions.</summary>
internal static class TestPacks
{
    /// <summary>
    /// Domain depends on nothing; Infrastructure and WebApi may depend on Domain. A layer that covers no type fails the run,
    /// so a test names the layers its fixture has, and gets all three when it names none.
    /// </summary>
    public static RulePack Layered(params string[] layers) => Named("test", layers);

    /// <summary>The same layered pack under the given name.</summary>
    public static RulePack Named(string name, params string[] layers) => new()
    {
        Name = name,
        Layers = new[]
        {
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"] },
            new LayerDefinition { Name = "Infrastructure", Id = "TEST-LAYER-INFRASTRUCTURE", Because = "Test layer Infrastructure.", Namespaces = ["**.Infrastructure.**"], AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "WebApi", Id = "TEST-LAYER-WEBAPI", Because = "Test layer WebApi.", Namespaces = ["**.WebApi.**"], AllowedDependencies = ["Domain"] },
        }.Where(l => layers.Length == 0 || Array.IndexOf(layers, l.Name) >= 0).ToList(),
    };
}
