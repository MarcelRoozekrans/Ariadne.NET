using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class LayerMapTests
{
    private static RulePack Pack(params LayerDefinition[] layers) => new() { Name = "p", Layers = [.. layers] };

    private static LayerDefinition Layer(string name, params string[] namespaces) => new() { Name = name, Namespaces = [.. namespaces] };

    private static INamedTypeSymbol Find(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) ?? throw new InvalidOperationException($"{metadataName} not found.");

    [Fact]
    public void TypeInOneLayer_ReturnsIt()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class Order { } }");
        var map = new LayerMap(Pack(Layer("Domain", "**.Domain.**"), Layer("Infra", "**.Infra.**")));

        var type = Find(compilation, "Shop.Domain.Order");

        map.LayerOf(type).Should().Be("Domain");
        map.LayersOf(type).Should().Equal("Domain");
    }

    [Fact]
    public void TypeInNoLayer_ReturnsNull()
    {
        var compilation = TestCompilation.Create("namespace Shop.Other { public class X { } }");
        var map = new LayerMap(Pack(Layer("Domain", "**.Domain.**")));

        var type = Find(compilation, "Shop.Other.X");

        map.LayerOf(type).Should().BeNull();
        map.LayersOf(type).Should().BeEmpty();
    }

    [Fact]
    public void TypeInTwoLayers_LayerOfNull_LayersOfBoth()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class Order { } }");
        var map = new LayerMap(Pack(Layer("Domain", "**.Domain.**"), Layer("Shop", "Shop.**")));

        var type = Find(compilation, "Shop.Domain.Order");

        map.LayerOf(type).Should().BeNull();
        map.LayersOf(type).Should().Equal("Domain", "Shop");
    }

    [Fact]
    public void ModularMonolithDottedLayer_Matches()
    {
        var compilation = TestCompilation.Create("namespace Shop.Orders.Public { public class X { } }");
        var map = new LayerMap(Pack(Layer("Module.Public", "**.Public.**")));

        map.LayerOf(Find(compilation, "Shop.Orders.Public.X")).Should().Be("Module.Public");
    }

    [Fact]
    public void LayerWithoutSelectors_MatchesNothing()
    {
        var compilation = TestCompilation.Create("namespace N { public class X { } }");
        var map = new LayerMap(Pack(new LayerDefinition { Name = "Empty" }));

        map.LayerOf(Find(compilation, "N.X")).Should().BeNull();
    }

    [Fact]
    public void NullArguments_Throw()
    {
        ((Action)(() => _ = new LayerMap(null!))).Should().Throw<ArgumentNullException>();
        var map = new LayerMap(Pack());
        ((Action)(() => map.LayerOf(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => map.LayersOf(null!))).Should().Throw<ArgumentNullException>();
    }

    // Interim: measures with GC.GetAllocatedBytesForCurrentThread, see ZeroAlloc-Net/ZeroAlloc.TestHelpers#106;
    // AllocationGate is not referenced in this repo yet.
    [Fact]
    public void LayerOf_CachedSymbol_AllocatesNothing()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class Order { } }");
        var map = new LayerMap(Pack(Layer("Domain", "**.Domain.**")));
        var type = Find(compilation, "Shop.Domain.Order");
        map.LayerOf(type).Should().Be("Domain");
        map.LayersOf(type);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            map.LayerOf(type);
            map.LayersOf(type);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }
}
