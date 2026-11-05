using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Rule packs for the fixture solutions.</summary>
internal static class TestPacks
{
    /// <summary>Domain depends on nothing; Infrastructure and WebApi may depend on Domain.</summary>
    public static RulePack Layered() => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"] },
            new LayerDefinition { Name = "Infrastructure", Id = "TEST-LAYER-INFRASTRUCTURE", Because = "Test layer Infrastructure.", Namespaces = ["**.Infrastructure.**"], AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "WebApi", Id = "TEST-LAYER-WEBAPI", Because = "Test layer WebApi.", Namespaces = ["**.WebApi.**"], AllowedDependencies = ["Domain"] },
        ],
    };
}
