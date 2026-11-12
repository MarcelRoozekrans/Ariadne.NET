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
    public void MatchesMember_WildcardMemberSegment()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop.Data
            {
                public class DbCommand { public virtual int ExecuteReader() => 0; public int Other() => 0; }
                public class SqlCommand : DbCommand { public override int ExecuteReader() => 0; }
            }
            namespace Shop.Web { public class Page { public int ExecuteScalar() => 0; } }
            """);
        var matcher = TargetMatcher.From(new TargetSelector { Members = ["Shop.Data.*.Execute*"] }, new LayerMap(Pack));

        bool Matches(string type, string member) =>
            matcher.MatchesMember(new MemberEdge(Find(compilation, "Shop.Data.SqlCommand"), Find(compilation, type).GetMembers(member)[0], default));

        Matches("Shop.Data.DbCommand", "ExecuteReader").Should().BeTrue();
        Matches("Shop.Data.SqlCommand", "ExecuteReader").Should().BeTrue();
        Matches("Shop.Data.DbCommand", "Other").Should().BeFalse();
        Matches("Shop.Web.Page", "ExecuteScalar").Should().BeFalse();
        matcher.Matches(Find(compilation, "Shop.Data.SqlCommand")).Should().BeFalse();
    }

    [Fact]
    public void NamespacesWithMembers_MatchesNoType()
    {
        var compilation = TestCompilation.Create(Source);
        var matcher = TargetMatcher.From(new TargetSelector { Namespaces = ["Shop.**"], Members = ["Shop.Domain.Order.Total"] }, new LayerMap(Pack));

        matcher.Matches(Find(compilation, "Shop.Domain.Order")).Should().BeFalse();
    }

    [Fact]
    public void LayersMixedWithMembers_Throws()
    {
        var act = () => TargetMatcher.From(new TargetSelector { Layers = ["Domain"], Members = ["A.B.C"] }, new LayerMap(Pack));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MixedTarget_Throws()
    {
        var act = () => TargetMatcher.From(new TargetSelector { Layers = ["Domain"], Namespaces = ["Shop"] }, new LayerMap(Pack));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("BaseTypes")]
    [InlineData("Interfaces")]
    [InlineData("Attributes")]
    public void LayersMixedWithCodeFeatureSelectors_Throws(string key)
    {
        var target = key switch
        {
            "BaseTypes" => new TargetSelector { Layers = ["Domain"], BaseTypes = ["**.Base"] },
            "Interfaces" => new TargetSelector { Layers = ["Domain"], Interfaces = ["**.IRepository"] },
            _ => new TargetSelector { Layers = ["Domain"], Attributes = ["**.ObsoleteAttribute"] },
        };

        var act = () => TargetMatcher.From(target, new LayerMap(Pack));

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

    [Fact]
    public void Layers_and_traits_intersect()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop.Domain { public sealed class S { } public abstract class A { } }
            namespace Shop.Web { public sealed class Page { } }
            """);
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "L", Id = "L-L", Because = "L.", Namespaces = ["Shop.Domain"] }],
        };
        var matcher = TargetMatcher.From(new TargetSelector { Layers = ["L"], Sealed = true }, new LayerMap(pack));

        matcher.Matches(Find(compilation, "Shop.Domain.S")).Should().BeTrue();
        matcher.Matches(Find(compilation, "Shop.Domain.A")).Should().BeFalse();
        matcher.Matches(Find(compilation, "Shop.Web.Page")).Should().BeFalse();
    }

    [Fact]
    public void Traits_alone_select_types()
    {
        var compilation = TestCompilation.Create(Source);
        var matcher = TargetMatcher.From(new TargetSelector { Nested = true }, new LayerMap(Pack));

        matcher.Matches(Find(compilation, "Shop.Domain.Order+Line")).Should().BeTrue();
        matcher.Matches(Find(compilation, "Shop.Domain.Order")).Should().BeFalse();
    }

    [Fact]
    public void Layers_with_namespaces_still_throws()
    {
        var act = () => TargetMatcher.From(new TargetSelector { Layers = ["Domain"], Namespaces = ["Shop"], Sealed = true }, new LayerMap(Pack));

        act.Should().Throw<ArgumentException>();
    }
}
