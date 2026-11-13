using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class SliceRulesTests
{
    private const string ShopModules = "Shop.(*).**";

    private const string OrdersToBilling = """
        namespace Shop.Billing { public class B { } }
        namespace Shop.Orders
        {
            using Shop.Billing;
            public class A { public B? X; public B? Y; }
        }
        """;

    private static RuleDefinition Independent(string[] matching, TargetSelector? allow = null, params RuleException[] exceptions) => new()
    {
        Id = "R-SLICES",
        Because = "Modules stay apart.",
        Kind = RuleKind.Slices,
        From = new TargetSelector(),
        Slices = new SliceDefinition { Matching = matching, Independent = true, Allow = allow },
        Exceptions = exceptions,
    };

    private static RulePack PackWith(RuleDefinition rule, params LayerDefinition[] layers) =>
        new() { Name = "test", Layers = layers, Rules = [rule] };

    [Fact]
    public void Independent_reports_each_cross_slice_reference()
    {
        var violations = Check(PackWith(Independent([ShopModules])), OrdersToBilling);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.SliceDependency);
        v.RuleId.Should().Be("R-SLICES");
        v.Because.Should().Be("Modules stay apart.");
        v.SourceType.Should().Be("Shop.Orders.A");
        v.Target.Should().Be("Shop.Billing.B");
        v.Location.Should().Be(new SourceLocation("Test.cs", 5, 29));
        v.ReferenceCount.Should().Be(2);
        v.TargetFrameworks.Should().BeEmpty();
        v.Message.Should().Be(
            "Type 'Shop.Orders.A' in slice 'Orders' depends on 'Shop.Billing.B' in slice 'Billing', and rule 'R-SLICES' keeps slices independent 2 times");
    }

    [Fact]
    public void Single_reference_has_no_count_suffix()
    {
        var v = Check(PackWith(Independent([ShopModules])), """
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            """).Should().ContainSingle().Subject;

        v.ReferenceCount.Should().Be(1);
        v.Message.Should().EndWith("and rule 'R-SLICES' keeps slices independent");
    }

    [Fact]
    public void Same_slice_reference_is_fine()
    {
        Check(PackWith(Independent([ShopModules])), """
            namespace Shop.Orders.Core { public class C { } }
            namespace Shop.Orders { public class A { public Shop.Orders.Core.C? X; } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void Allow_by_namespace_and_by_layer()
    {
        const string source = """
            namespace Shop.Billing.Public { public class X { } }
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public class A { public Shop.Billing.Public.X? P; public Shop.Billing.B? Q; } }
            """;
        var byNamespace = PackWith(Independent([ShopModules], new TargetSelector { Namespaces = ["Shop.*.Public.**"] }));
        var byLayer = PackWith(
            Independent([ShopModules], new TargetSelector { Layers = ["Public"] }),
            new LayerDefinition { Name = "Public", Id = "L-PUBLIC", Because = "Public APIs.", Namespaces = ["**.Public.**"] });

        Check(byNamespace, source).Should().ContainSingle().Which.Target.Should().Be("Shop.Billing.B");
        Check(byLayer, source).Should().ContainSingle().Which.Target.Should().Be("Shop.Billing.B");
    }

    [Fact]
    public void Exception_exempts_the_source()
    {
        var pack = PackWith(Independent([ShopModules], exceptions: new RuleException { Types = ["Shop.Orders.A"], Because = "Legacy." }));

        Check(pack, OrdersToBilling).Should().BeEmpty();
    }

    [Fact]
    public void Excepted_type_is_still_a_target()
    {
        var pack = PackWith(Independent([ShopModules], exceptions: new RuleException { Types = ["Shop.Billing.B"], Because = "Legacy." }));

        var v = Check(pack, """
            namespace Shop.Billing { public class B { public Shop.Orders.A? Back; } }
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            """).Should().ContainSingle().Subject;

        v.SourceType.Should().Be("Shop.Orders.A");
        v.Target.Should().Be("Shop.Billing.B");
    }

    [Fact]
    public void Same_name_in_two_assemblies_stays_apart()
    {
        var one = TestCompilation.Create(("One.cs", """
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            """)).WithAssemblyName("Shop.One");
        var two = TestCompilation.Create(("Two.cs", """
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            """)).WithAssemblyName("Shop.Two");

        var violations = Check(PackWith(Independent([ShopModules])), (two, null), (one, null));

        violations.Should().HaveCount(2);
        violations.Select(v => v.Location!.Value.FilePath).Should().BeEquivalentTo("One.cs", "Two.cs");
        violations.Should().OnlyContain(v => v.ReferenceCount == 1 && string.Equals(v.SourceType, "Shop.Orders.A", StringComparison.Ordinal));
    }

    [Fact]
    public void Reference_to_a_type_outside_the_run_has_no_slice()
    {
        var lib = TestCompilation.Create("namespace Lib.Billing.Core { public class X { } }").WithAssemblyName("Lib");
        var shop = TestCompilation.Create(
            [lib.ToMetadataReference()],
            ("Test.cs", """
                namespace Shop.Orders
                {
                    public class A
                    {
                        public Lib.Billing.Core.X? X;
                        public System.Collections.Generic.List<int>? L;
                    }
                }
                """));

        Check(PackWith(Independent(["**.(*).Core.**", "**.(*).**"])), (shop, null)).Should().BeEmpty();
    }

    [Fact]
    public void Nested_type_counts_in_its_outer_slice()
    {
        var v = Check(PackWith(Independent(["Shop.(**)"])), """
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders
            {
                public class C { }
                public class A { public class Inner { public C? Own; public Shop.Billing.B? Other; } }
            }
            """).Should().ContainSingle().Subject;

        v.SourceType.Should().Be("Shop.Orders.A.Inner");
        v.Message.Should().Contain("in slice 'Orders' depends on");
    }

    [Fact]
    public void Type_in_two_frameworks_counts_once()
    {
        var violations = Check(
            PackWith(Independent([ShopModules])),
            (TestCompilation.Create(OrdersToBilling), "net8.0"),
            (TestCompilation.Create(OrdersToBilling), "net10.0"));

        var v = violations.Should().ContainSingle().Subject;
        v.ReferenceCount.Should().Be(2);
        v.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void Edge_in_one_framework_reports_that_framework()
    {
        var violations = Check(
            PackWith(Independent([ShopModules])),
            (TestCompilation.Create(OrdersToBilling), "net8.0"),
            (TestCompilation.Create("""
                namespace Shop.Billing { public class B { } }
                namespace Shop.Orders { public class A { } }
                """), "net10.0"));

        violations.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net8.0");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Exception_is_evaluated_for_each_framework_variant_of_the_source(bool exceptedFirst)
    {
        var pack = PackWith(Independent([ShopModules], exceptions: new RuleException { Sealed = true, Because = "Sealed variants are legacy." }));
        var excepted = (TestCompilation.Create("""
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public sealed class A { public Shop.Billing.B? X; } }
            """), "net8.0");
        var checkedVariant = (TestCompilation.Create("""
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            """), "net10.0");

        var violations = exceptedFirst ? Check(pack, excepted, checkedVariant) : Check(pack, checkedVariant, excepted);

        violations.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net10.0");
    }

    [Fact]
    public void Single_target_project_counts_in_every_framework_scope()
    {
        var billing = TestCompilation.Create("namespace Shop.Billing { public class B { } }").WithAssemblyName("Shop.Billing");
        var orders = TestCompilation.Create(
            [billing.ToMetadataReference()],
            ("Orders.cs", "namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }")).WithAssemblyName("Shop.Orders");

        var violations = Check(PackWith(Independent([ShopModules])), (billing, "net8.0"), (billing, "net10.0"), (orders, null));

        violations.Should().ContainSingle().Which.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void Member_edges_do_not_count()
    {
        const string source = """
            namespace Lib { public class Factory { public Shop.Billing.Thing Create() => new(); } }
            namespace Shop.Billing { public class Thing { public void Do() { } } }
            namespace Shop.Orders { public class A { public void Run(Lib.Factory f) => f.Create().Do(); } }
            """;
        var filterPack = new RulePack
        {
            Name = "members",
            Rules = [new RuleDefinition { Id = "M", Because = "M.", Kind = RuleKind.Forbid, From = new TargetSelector { Namespaces = ["**"] }, To = new TargetSelector { Members = ["**.Do"] } }],
        };
        var graph = TypeGraphBuilder.Build(TestCompilation.Create(source), new HashSet<SyntaxTree>(), MemberNameFilter.From(filterPack), CancellationToken.None);
        graph.MemberEdges.Should().Contain(e => e.Source.ToDisplayString() == "Shop.Orders.A");

        // A member use also gives a type edge to the member's type, so only a graph stripped of its type edges shows that the
        // member edges themselves are not walked.
        var memberEdgesOnly = new TypeGraph([], graph.MemberEdges, graph.SourceTypes, graph.SourceTypeLocations);
        var pack = PackWith(Independent([ShopModules]));

        SliceRules.Check(new LayerMap(pack), pack, [new ScopedGraph(graph, null, "Shop", [])]).Should().ContainSingle();
        SliceRules.Check(new LayerMap(pack), pack, [new ScopedGraph(memberEdgesOnly, null, "Shop", [])]).Should().BeEmpty();
    }

    [Fact]
    public void First_matching_pattern_names_the_slice()
    {
        var v = Check(PackWith(Independent(["Shop.(*).Core.**", "Shop.(**)"])), """
            namespace Shop.Billing.Core { public class B { } }
            namespace Shop.Orders.Web { public class A { public Shop.Billing.Core.B? X; } }
            """).Should().ContainSingle().Subject;

        v.Message.Should().Contain("in slice 'Orders.Web' depends on 'Shop.Billing.Core.B' in slice 'Billing'");
    }

    [Fact]
    public void Global_namespace_type_has_no_slice()
    {
        Check(PackWith(Independent(["(**)"])), """
            public class G { public Shop.Billing.B? X; }
            namespace Shop.Billing { public class B { public G? Back; } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void Validator_reports_slice_dependencies()
    {
        var pack = PackWith(Independent([ShopModules]));
        var report = new ArchitectureValidator().Validate(pack, TestCompilation.Create(OrdersToBilling));

        report.Violations.Should().ContainSingle(v => v.Kind == ViolationKind.SliceDependency)
            .Which.ReferenceCount.Should().Be(2);
    }

    [Fact]
    public void Kinds_have_their_numbers()
    {
        ((int)ViolationKind.SliceCycle).Should().Be(14);
        ((int)ViolationKind.SliceDependency).Should().Be(15);
    }

    [Fact]
    public void Independent_edge_in_a_referenced_variant_counts_in_the_consumer_scope()
    {
        var orders = Named("Orders.Core", "namespace Shop.Orders { public class Order { } }");
        var billing8 = Named("Billing", "namespace Shop.Billing { public class Invoice { public Shop.Orders.Order? O; } }", orders);
        var infra10 = Named(
            "Orders.Infrastructure",
            "namespace Shop.Orders.Infrastructure { public class Repo { public Shop.Billing.Invoice? I; } }",
            billing8,
            orders);

        var violations = Check(PackWith(Independent([ShopModules])), (orders, null), (billing8, "net8.0"), (infra10, "net10.0"));

        violations.Should().HaveCount(2);
        violations.Single(v => string.Equals(v.SourceType, "Shop.Billing.Invoice", StringComparison.Ordinal)).TargetFrameworks.Should().BeEmpty();
        violations.Single(v => string.Equals(v.SourceType, "Shop.Orders.Infrastructure.Repo", StringComparison.Ordinal)).TargetFrameworks.Should().Equal("net10.0");
    }

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, string source) =>
        Check(pack, (TestCompilation.Create(source), null));

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, params (CSharpCompilation Compilation, string? Framework)[] inputs)
    {
        var projects = inputs
            .Select(i => new AnalyzedProject(i.Compilation.AssemblyName ?? string.Empty, i.Framework, i.Compilation))
            .ToList();
        var graphs = projects
            .Select(p => TypeGraphBuilder.Build(p.Compilation, new HashSet<SyntaxTree>(), CancellationToken.None))
            .ToList();
        return SliceRules.Check(new LayerMap(pack), pack, ScopedGraph.Of(projects, graphs));
    }

    private static CSharpCompilation Named(string assembly, string source, params CSharpCompilation[] references) =>
        TestCompilation.Create([.. references.Select(r => (MetadataReference)r.ToMetadataReference())], (assembly + ".cs", source))
            .WithAssemblyName(assembly);
}
