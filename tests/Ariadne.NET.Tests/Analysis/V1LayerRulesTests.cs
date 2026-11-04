using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class V1LayerRulesTests
{
    private static readonly RulePack Pack = new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain" },
            new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
        ],
    };

    [Fact]
    public void AllowedDependency_NoViolation() =>
        Check("""
            namespace Shop.Domain { public class Order { } }
            namespace Shop.Application { public class Handler { public Shop.Domain.Order? O; } }
            """).Should().BeEmpty();

    [Fact]
    public void ForbiddenDependency_OneViolation()
    {
        var violations = Check("""
            namespace Shop.Infrastructure { public class Store { } }
            namespace Shop.Domain
            {
                public class Order { public Shop.Infrastructure.Store? S; }
            }
            """);

        var v = violations.Should().ContainSingle().Subject;
        v.RuleId.Should().Be("layer/Domain");
        v.Kind.Should().Be(ViolationKind.DependencyNotAllowed);
        v.SourceType.Should().Be("Shop.Domain.Order");
        v.Target.Should().Be("Shop.Infrastructure.Store");
        v.Location.Should().Be(new SourceLocation("Test.cs", 4, 53));
        v.ReferenceCount.Should().Be(1);
        v.Message.Should().Be("Layer 'Domain' is not allowed to depend on 'Infrastructure': Shop.Domain.Order references Shop.Infrastructure.Store");
    }

    [Fact]
    public void SameLayer_NoViolation() =>
        Check("""
            namespace Shop.Domain { public class A { } }
            namespace Shop.Domain.Sub { public class B { public Shop.Domain.A? A; } }
            """).Should().BeEmpty();

    [Fact]
    public void TargetWithoutLayer_NoViolation() =>
        Check("namespace Shop.Domain { public class Order { public System.Text.StringBuilder? B; } }")
            .Should().BeEmpty();

    [Fact]
    public void SourceWithoutLayer_NoViolation() =>
        Check("""
            namespace Shop.Infrastructure { public class Store { } }
            namespace Shop.Misc { public class Thing { public Shop.Infrastructure.Store? S; } }
            """).Should().BeEmpty();

    [Fact]
    public void MetadataTypeInLayerNamespace_IsConstrained()
    {
        var library = TestCompilation.Create("namespace Other.Infrastructure { public class Ext { } }");
        var compilation = TestCompilation.Create(
            [library.ToMetadataReference()],
            ("Test.cs", "namespace Shop.Domain { public class Order { public Other.Infrastructure.Ext? E; } }"));

        var violations = Check(compilation);

        var v = violations.Should().ContainSingle().Subject;
        v.Target.Should().Be("Other.Infrastructure.Ext");
        v.RuleId.Should().Be("layer/Domain");
    }

    [Fact]
    public void LastMatchingSegmentWins()
    {
        var violations = Check("""
            namespace Shop.Domain.Infrastructure { public class X { } }
            namespace Shop.Domain { public class Order { public Shop.Domain.Infrastructure.X? X; } }
            """);

        violations.Should().ContainSingle().Which.Message.Should().StartWith("Layer 'Domain' is not allowed to depend on 'Infrastructure'");
    }

    [Fact]
    public void CaseInsensitiveLayerMatch()
    {
        var violations = Check("""
            namespace Shop.infrastructure { public class Store { } }
            namespace Shop.DOMAIN { public class Order { public Shop.infrastructure.Store? S; } }
            """);

        violations.Should().ContainSingle().Which.RuleId.Should().Be("layer/Domain");
    }

    [Fact]
    public void ThirtyReferences_OneViolationWithCount30()
    {
        var fields = string.Concat(Enumerable.Range(0, 30).Select(i => $"public Shop.Infrastructure.Store? F{i}; "));
        var violations = Check($$"""
            namespace Shop.Infrastructure { public class Store { } }
            namespace Shop.Domain { public class Order { {{fields}} } }
            """);

        var v = violations.Should().ContainSingle().Subject;
        v.ReferenceCount.Should().Be(30);
        v.Message.Should().EndWith("Shop.Infrastructure.Store 30 times");
        v.Location.Should().Be(new SourceLocation("Test.cs", 2, 73)); // the first field's type name, not the last
    }

    [Fact]
    public void TwoTargets_TwoViolations()
    {
        var violations = Check("""
            namespace Shop.Infrastructure { public class A { } public class B { } }
            namespace Shop.Domain { public class Order { public Shop.Infrastructure.A? A; public Shop.Infrastructure.B? B; } }
            """);

        violations.Should().HaveCount(2);
        violations.Select(v => v.Target).Should().Equal("Shop.Infrastructure.A", "Shop.Infrastructure.B");
    }

    // Interim: measures with GC.GetAllocatedBytesForCurrentThread because the ZeroAlloc.TestHelpers source fails this repo's
    // analyzers, tracked as ZeroAlloc-Net/ZeroAlloc.TestHelpers#106. Switch to AllocationGate once that issue ships.
    [Fact]
    public void LayerOf_CachedSymbol_AllocatesNothing()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class Order { } }");
        var type = compilation.GetTypeByMetadataName("Shop.Domain.Order")!;
        var map = new V1LayerMap(Pack);

        var beforeMiss = GC.GetAllocatedBytesForCurrentThread();
        var first = map.LayerOf(type);
        var missBytes = GC.GetAllocatedBytesForCurrentThread() - beforeMiss;

        first.Should().Be("Domain");
        missBytes.Should().BeGreaterThan(0, "the first call inserts into the cache");

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            map.LayerOf(type);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [Fact]
    public void LayerOf_TypeWithoutLayer_IsNullAndCached()
    {
        var compilation = TestCompilation.Create("namespace Shop.Misc { public class Thing { } }");
        var type = compilation.GetTypeByMetadataName("Shop.Misc.Thing")!;
        var map = new V1LayerMap(Pack);
        map.LayerOf(type).Should().BeNull();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            map.LayerOf(type);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
        map.LayerOf(type).Should().BeNull();
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source) => Check(TestCompilation.Create(source));

    private static IReadOnlyList<ArchitectureViolation> Check(CSharpCompilation compilation)
    {
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        return V1LayerRules.Check(graph, new V1LayerMap(Pack), Pack);
    }
}
