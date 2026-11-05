using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class LayerRulesTests
{
    private static readonly RulePack Pack = new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"] },
            new LayerDefinition { Name = "Application", Id = "TEST-LAYER-APPLICATION", Because = "Test layer Application.", Namespaces = ["**.Application.**"], AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", Id = "TEST-LAYER-INFRASTRUCTURE", Because = "Test layer Infrastructure.", Namespaces = ["**.Infrastructure.**"], AllowedDependencies = ["Domain", "Application"] },
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
        v.RuleId.Should().Be("TEST-LAYER-DOMAIN");
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
        v.RuleId.Should().Be("TEST-LAYER-DOMAIN");
    }

    [Fact]
    public void TwoLayerSegments_IsLayerOverlap()
    {
        var violations = Check("""
            namespace Shop.Domain.Infrastructure { public class X { } }
            namespace Shop.Domain { public class Order { public Shop.Domain.Infrastructure.X? X; } }
            """);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.LayerOverlap);
        v.RuleId.Should().Be("TEST-LAYER-DOMAIN+TEST-LAYER-INFRASTRUCTURE");
        v.SourceType.Should().Be("Shop.Domain.Infrastructure.X");
        v.Message.Should().Be("Type 'Shop.Domain.Infrastructure.X' is covered by layers 'Domain', 'Infrastructure'");
    }

    [Fact]
    public void LowerCaseNamespace_NotInLayer() =>
        Check("""
            namespace Shop.infrastructure { public class Store { } }
            namespace Shop.domain { public class Order { public Shop.infrastructure.Store? S; } }
            """).Should().BeEmpty();

    [Fact]
    public void Violation_CarriesLayerIdAndBecause()
    {
        var v = Check("""
            namespace Shop.Infrastructure { public class Store { } }
            namespace Shop.Domain { public class Order { public Shop.Infrastructure.Store? S; } }
            """).Should().ContainSingle().Subject;

        v.RuleId.Should().Be("TEST-LAYER-DOMAIN");
        v.Because.Should().Be("Test layer Domain.");
    }

    [Fact]
    public void AssemblySelectedLayer_IsConstrained()
    {
        var pack = new RulePack
        {
            Name = "test",
            Layers =
            [
                new LayerDefinition { Name = "Core", Id = "T-CORE", Because = "Core is the centre.", Assemblies = ["TestAssembly"] },
                new LayerDefinition { Name = "Host", Id = "T-HOST", Because = "Host is the edge.", Assemblies = ["Shop.Lib"], AllowedDependencies = ["Core"] },
            ],
        };
        var library = TestCompilation.Create("namespace Shop.Host { public class Web { } }").WithAssemblyName("Shop.Lib");
        var compilation = TestCompilation.Create(
            [library.ToMetadataReference()],
            ("Test.cs", "namespace Shop.Core { public class Engine { public Shop.Host.Web? W; } }"));

        var v = Check(compilation, pack).Should().ContainSingle().Subject;

        v.RuleId.Should().Be("T-CORE");
        v.Because.Should().Be("Core is the centre.");
        v.Message.Should().Be("Layer 'Core' is not allowed to depend on 'Host': Shop.Core.Engine references Shop.Host.Web");
    }

    [Fact]
    public void OverlappingSourceType_OneOverlap_NoDependencyViolation()
    {
        var violations = Check("""
            namespace Shop.Infrastructure { public class Store { } }
            namespace Shop.Application { public class Other { } }
            namespace Shop.Domain.Application
            {
                public class Mixed { public Shop.Infrastructure.Store? S; public Shop.Application.Other? O; public Shop.Infrastructure.Store? T; }
            }
            """);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.LayerOverlap);
        v.SourceType.Should().Be("Shop.Domain.Application.Mixed");
        v.Target.Should().BeNull();
        v.Because.Should().Be("A type must belong to at most one layer.");
        v.Location.Should().Be(new SourceLocation("Test.cs", 5, 18));
    }

    [Fact]
    public void OverlappingMetadataTarget_OneOverlapWithoutLocation()
    {
        var library = TestCompilation.Create("namespace Other.Domain.Infrastructure { public class Ext { } }");
        var compilation = TestCompilation.Create(
            [library.ToMetadataReference()],
            ("Test.cs", "namespace Shop.Domain { public class Order { public Other.Domain.Infrastructure.Ext? A; public Other.Domain.Infrastructure.Ext? B; } }"));

        var v = Check(compilation).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.LayerOverlap);
        v.RuleId.Should().Be("TEST-LAYER-DOMAIN+TEST-LAYER-INFRASTRUCTURE");
        v.SourceType.Should().Be("Other.Domain.Infrastructure.Ext");
        v.Location.Should().BeNull();
        v.Target.Should().BeNull();
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
        var map = new LayerMap(Pack);

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

    // Interim: see the note on LayerOf_CachedSymbol_AllocatesNothing, ZeroAlloc-Net/ZeroAlloc.TestHelpers#106.
    [Fact]
    public void LayerOf_TypeWithoutLayer_IsNullAndCached()
    {
        var compilation = TestCompilation.Create("namespace Shop.Misc { public class Thing { } }");
        var type = compilation.GetTypeByMetadataName("Shop.Misc.Thing")!;
        var map = new LayerMap(Pack);

        var beforeMiss = GC.GetAllocatedBytesForCurrentThread();
        var first = map.LayerOf(type);
        var missBytes = GC.GetAllocatedBytesForCurrentThread() - beforeMiss;

        first.Should().BeNull();
        missBytes.Should().BeGreaterThan(0, "the null result is inserted into the cache");

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            map.LayerOf(type);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
        map.LayerOf(type).Should().BeNull();
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source) => Check(TestCompilation.Create(source));

    private static IReadOnlyList<ArchitectureViolation> Check(CSharpCompilation compilation) => Check(compilation, Pack);

    private static IReadOnlyList<ArchitectureViolation> Check(CSharpCompilation compilation, RulePack pack)
    {
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        return LayerRules.Check(graph, new LayerMap(pack), pack);
    }
}
