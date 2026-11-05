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
            new LayerDefinition { Name = "Domain" },
            new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "WebApi", AllowedDependencies = ["Domain"] },
        ],
    };
}
