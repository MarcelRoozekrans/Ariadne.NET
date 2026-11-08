using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.Analysis;

public class LayerProbeTests
{
    private static RulePack Pack(params LayerDefinition[] layers) => new() { Name = "test", Layers = layers };

    private static LayerDefinition Layer(string name, string[]? namespaces = null, string[]? assemblies = null) => new()
    {
        Name = name,
        Id = $"TEST-{name}",
        Because = "Test layer.",
        Namespaces = namespaces ?? [],
        Assemblies = assemblies ?? [],
    };

    [Fact]
    public void An_assembly_only_layer_matches_every_namespace_of_the_assembly()
    {
        var pack = Pack(Layer("Domain", assemblies: ["Daedalus.Domain"]));

        LayerProbe.LayersOf(pack, "Daedalus.Domain", "Anything.At.All").Should().Equal("Domain");
        LayerProbe.LayersOf(pack, "Other", "Anything.At.All").Should().BeEmpty();
    }

    [Fact]
    public void A_namespace_only_layer_matches_the_namespace_in_any_assembly()
    {
        var pack = Pack(Layer("Domain", namespaces: ["Shop.Domain.**"]));

        LayerProbe.LayersOf(pack, "Whatever", "Shop.Domain.Entities").Should().Equal("Domain");
        LayerProbe.LayersOf(pack, "Whatever", "Shop.Web").Should().BeEmpty();
    }

    [Fact]
    public void A_layer_with_both_lists_needs_both()
    {
        var pack = Pack(Layer("Runtime", namespaces: ["Daedalus.Agents.Runtime.**"], assemblies: ["Daedalus.Agents"]));

        LayerProbe.LayersOf(pack, "Daedalus.Agents", "Daedalus.Agents.Runtime.Retrieval").Should().Equal("Runtime");
        LayerProbe.LayersOf(pack, "Daedalus.Agents", "Daedalus.Agents.Contracts").Should().BeEmpty();
        LayerProbe.LayersOf(pack, "Other", "Daedalus.Agents.Runtime.Retrieval").Should().BeEmpty();
    }

    [Fact]
    public void A_layer_with_neither_list_selects_nothing() =>
        LayerProbe.LayersOf(Pack(Layer("Empty")), "Any", "Any.Namespace").Should().BeEmpty();

    [Fact]
    public void Overlapping_layers_are_returned_in_pack_order()
    {
        var pack = Pack(
            Layer("Wide", assemblies: ["Shop.*"]),
            Layer("Other", assemblies: ["Nope"]),
            Layer("Narrow", namespaces: ["Shop.Domain"]));

        LayerProbe.LayersOf(pack, "Shop.Core", "Shop.Domain").Should().Equal("Wide", "Narrow");
    }
}
