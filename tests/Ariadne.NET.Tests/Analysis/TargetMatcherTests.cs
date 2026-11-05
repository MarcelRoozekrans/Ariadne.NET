using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class TargetMatcherTests
{
    private const string Source = """
        namespace Shop.Domain { public class Order { public class Line { } } }
        namespace Shop.Web { public class Page { } }
        """;

    private static readonly RulePack Pack = new()
    {
        Name = "p",
        Layers = [new LayerDefinition { Name = "Domain", Id = "L-DOMAIN", Because = "Domain.", Namespaces = ["**.Domain.**"] }],
    };

    private static INamedTypeSymbol Find(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) ?? throw new InvalidOperationException($"{metadataName} not found.");

    [Fact]
    public void LayersTarget_MatchesTheLayersTypes()
    {
        var compilation = TestCompilation.Create(Source);
        var matcher = TargetMatcher.From(new TargetSelector { Layers = ["domain"] }, new LayerMap(Pack));

        matcher.Matches(Find(compilation, "Shop.Domain.Order")).Should().BeTrue();
        matcher.Matches(Find(compilation, "Shop.Web.Page")).Should().BeFalse();
    }

    [Fact]
    public void SelectorTarget_MatchesBySelector()
    {
        var compilation = TestCompilation.Create(Source);
        var matcher = TargetMatcher.From(new TargetSelector { Types = ["**.Order.Line"] }, new LayerMap(Pack));

        matcher.Matches(Find(compilation, "Shop.Domain.Order+Line")).Should().BeTrue();
        matcher.Matches(Find(compilation, "Shop.Domain.Order")).Should().BeFalse();
    }

    [Fact]
    public void MixedTarget_Throws()
    {
        var act = () => TargetMatcher.From(new TargetSelector { Layers = ["Domain"], Namespaces = ["Shop"] }, new LayerMap(Pack));

        act.Should().Throw<ArgumentException>();
    }

    // Interim: measures with GC.GetAllocatedBytesForCurrentThread, see ZeroAlloc-Net/ZeroAlloc.TestHelpers#106;
    // AllocationGate is not referenced in this repo yet.
    [Fact]
    public void Matches_CachedSymbol_AllocatesNothing()
    {
        var compilation = TestCompilation.Create(Source);
        var layers = new LayerMap(Pack);
        var byNamespace = TargetMatcher.From(new TargetSelector { Namespaces = ["Shop.**"] }, layers);
        var byType = TargetMatcher.From(new TargetSelector { Types = ["**.Order.Line"] }, layers);
        var line = Find(compilation, "Shop.Domain.Order+Line");
        byNamespace.Matches(line).Should().BeTrue();
        byType.Matches(line).Should().BeTrue();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            byNamespace.Matches(line);
            byType.Matches(line);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [Fact]
    public void Names_AreComputedOncePerSymbol()
    {
        var compilation = TestCompilation.Create(Source);
        var names = new TypeNames();
        var line = Find(compilation, "Shop.Domain.Order+Line");

        names.FullNameOf(line).Should().Be("Shop.Domain.Order.Line");
        names.NamespaceOf(line).Should().Be("Shop.Domain");
        names.FullNameOf(line).Should().BeSameAs(names.FullNameOf(line));
        names.NamespaceOf(line).Should().BeSameAs(names.NamespaceOf(Find(compilation, "Shop.Domain.Order")));
    }
}
