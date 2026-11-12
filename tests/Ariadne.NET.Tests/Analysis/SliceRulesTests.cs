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

        SliceRules.Check(new LayerMap(pack), pack, [(graph, null)]).Should().ContainSingle();
        SliceRules.Check(new LayerMap(pack), pack, [(memberEdgesOnly, null)]).Should().BeEmpty();
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

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, string source) =>
        Check(pack, (TestCompilation.Create(source), null));

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, params (CSharpCompilation Compilation, string? Framework)[] inputs)
    {
        var graphs = inputs
            .Select(i => (TypeGraphBuilder.Build(i.Compilation, new HashSet<SyntaxTree>(), CancellationToken.None), i.Framework))
            .ToList();
        return SliceRules.Check(new LayerMap(pack), pack, graphs);
    }
}
