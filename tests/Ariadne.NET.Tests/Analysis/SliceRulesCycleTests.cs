using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class SliceRulesCycleTests
{
    private const string ShopModules = "Shop.(*).**";

    private const string TwoSliceCycle = """
        namespace Shop.Billing { public class B { public Shop.Orders.A? Back; } }
        namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
        """;

    private static RuleDefinition Acyclic(TargetSelector? allow = null) => new()
    {
        Id = "R-ACYCLIC",
        Because = "Slices form a hierarchy.",
        Kind = RuleKind.Slices,
        From = new TargetSelector(),
        Slices = new SliceDefinition { Matching = [ShopModules], FreeOfCycles = true, Allow = allow },
    };

    private static RulePack PackWith(RuleDefinition rule) => new() { Name = "test", Rules = [rule] };

    [Fact]
    public void Two_slice_cycle_reports_both_edges()
    {
        var violations = Check(PackWith(Acyclic()), TwoSliceCycle);

        violations.Should().HaveCount(2).And.OnlyContain(v => v.Kind == ViolationKind.SliceCycle && string.Equals(v.RuleId, "R-ACYCLIC", StringComparison.Ordinal));
        violations.Select(v => v.SourceType).Should().BeEquivalentTo("Shop.Billing.B", "Shop.Orders.A");
        var orders = violations.Single(v => string.Equals(v.SourceType, "Shop.Orders.A", StringComparison.Ordinal));
        orders.Target.Should().Be("Shop.Billing.B");
        orders.Because.Should().Be("Slices form a hierarchy.");
        orders.ReferenceCount.Should().Be(1);
        orders.TargetFrameworks.Should().BeEmpty();
        orders.Message.Should().Be(
            "Slice 'Orders' depends on slice 'Billing' (first through 'Shop.Orders.A' -> 'Shop.Billing.B'), and slices Billing, Orders form a cycle, which rule 'R-ACYCLIC' forbids");
        violations.Single(v => string.Equals(v.SourceType, "Shop.Billing.B", StringComparison.Ordinal)).Message.Should().Be(
            "Slice 'Billing' depends on slice 'Orders' (first through 'Shop.Billing.B' -> 'Shop.Orders.A'), and slices Billing, Orders form a cycle, which rule 'R-ACYCLIC' forbids");
    }

    [Fact]
    public void Three_slice_cycle_reports_three_edges()
    {
        var violations = Check(PackWith(Acyclic()), """
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            namespace Shop.Billing { public class B { public Shop.Shipping.S? X; } }
            namespace Shop.Shipping { public class S { public Shop.Orders.A? X; } }
            """);

        violations.Should().HaveCount(3).And.OnlyContain(v => v.Kind == ViolationKind.SliceCycle);
        violations.Select(v => v.SourceType).Should().BeEquivalentTo("Shop.Orders.A", "Shop.Billing.B", "Shop.Shipping.S");
        violations.Should().OnlyContain(v => v.Message.Contains("slices Billing, Orders, Shipping form a cycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Edge_into_a_component_is_not_reported()
    {
        var violations = Check(PackWith(Acyclic()), TwoSliceCycle + """

            namespace Shop.Shipping { public class S { public Shop.Orders.A? X; } }
            """);

        violations.Should().HaveCount(2);
        violations.Should().NotContain(v => string.Equals(v.SourceType, "Shop.Shipping.S", StringComparison.Ordinal));
        violations.Should().OnlyContain(v => !v.Message.Contains("Shipping", StringComparison.Ordinal));
    }

    [Fact]
    public void Acyclic_slices_report_nothing()
    {
        Check(PackWith(Acyclic()), """
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; public Shop.Shipping.S? Y; } }
            namespace Shop.Billing { public class B { public Shop.Shipping.S? X; } }
            namespace Shop.Shipping { public class S { } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void Cycle_across_graphs_is_found()
    {
        var core = TestCompilation.Create(("Core.cs", """
            namespace Shop.Orders.Core { public class O { public Shop.Orders.Core.P? Own; } public class P { } }
            """)).WithAssemblyName("Shop.Orders.Core");
        var billing = TestCompilation.Create(
            [core.ToMetadataReference()],
            ("Billing.cs", "namespace Shop.Billing { public class Bill { public Shop.Orders.Core.O? X; } }")).WithAssemblyName("Shop.Billing");
        var infrastructure = TestCompilation.Create(
            [core.ToMetadataReference(), billing.ToMetadataReference()],
            ("Infra.cs", "namespace Shop.Orders.Infrastructure { public class I { public Shop.Billing.Bill? X; } }")).WithAssemblyName("Shop.Orders.Infrastructure");
        var pack = PackWith(Acyclic());

        Check(pack, (billing, null)).Should().BeEmpty();
        Check(pack, (infrastructure, null)).Should().BeEmpty();

        var violations = Check(pack, (core, null), (billing, null), (infrastructure, null));

        violations.Should().HaveCount(2).And.OnlyContain(v => v.Kind == ViolationKind.SliceCycle);
        violations.Select(v => v.SourceType).Should().BeEquivalentTo("Shop.Billing.Bill", "Shop.Orders.Infrastructure.I");
    }

    [Fact]
    public void Opposite_edges_in_different_frameworks_are_no_cycle()
    {
        const string ordersOnly = """
            namespace Shop.Billing { public class B { } }
            namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }
            """;
        const string billingOnly = """
            namespace Shop.Billing { public class B { public Shop.Orders.A? Back; } }
            namespace Shop.Orders { public class A { } }
            """;

        Check(PackWith(Acyclic()), (TestCompilation.Create(ordersOnly), "net8.0"), (TestCompilation.Create(billingOnly), "net10.0"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Cycle_with_a_single_target_project_reports_the_framework_that_holds_it()
    {
        var billingStub = TestCompilation.Create(("Billing.cs", "namespace Shop.Billing { public class B { } }")).WithAssemblyName("Shop.Billing");
        var orders = TestCompilation.Create(
            [billingStub.ToMetadataReference()],
            ("Orders.cs", "namespace Shop.Orders { public class A { public Shop.Billing.B? X; } }")).WithAssemblyName("Shop.Orders");
        var billing8 = TestCompilation.Create(
            [orders.ToMetadataReference()],
            ("Billing.cs", "namespace Shop.Billing { public class B { public Shop.Orders.A? Back; } }")).WithAssemblyName("Shop.Billing");

        var violations = Check(PackWith(Acyclic()), (orders, null), (billing8, "net8.0"), (billingStub, "net10.0"));

        violations.Should().HaveCount(2).And.OnlyContain(v => v.Kind == ViolationKind.SliceCycle);
        violations.Should().OnlyContain(v => v.TargetFrameworks.SequenceEqual(new[] { "net8.0" }));
    }

    [Fact]
    public void Source_is_the_earliest_reference_from_the_slice()
    {
        var violations = Check(PackWith(Acyclic()), """
            namespace Shop.Orders
            {
                public class Z { public Shop.Billing.C? First; }
                public class A { public Shop.Billing.B? X; public Shop.Billing.C? Y; }
            }
            namespace Shop.Billing { public class B { public Shop.Orders.A? Back; } public class C { } }
            """);

        var v = violations.Single(v => v.Message.StartsWith("Slice 'Orders'", StringComparison.Ordinal));
        v.SourceType.Should().Be("Shop.Orders.Z");
        v.Target.Should().Be("Shop.Billing.C");
        v.Location.Should().Be(new SourceLocation("Test.cs", 3, 42));
        v.ReferenceCount.Should().Be(3);
        v.Message.Should().Contain("(first through 'Shop.Orders.Z' -> 'Shop.Billing.C')");
    }

    [Fact]
    public void Allowed_edges_do_not_form_cycles()
    {
        var pack = PackWith(Acyclic(new TargetSelector { Namespaces = ["Shop.*.Public.**"] }));

        Check(pack, """
            namespace Shop.Billing.Public { public class P { } }
            namespace Shop.Billing { public class B { public Shop.Orders.A? Back; } }
            namespace Shop.Orders { public class A { public Shop.Billing.Public.P? X; } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void Validator_reports_slice_cycles()
    {
        var pack = PackWith(Acyclic());
        var report = new ArchitectureValidator().Validate(pack, TestCompilation.Create(TwoSliceCycle));

        report.Violations.Where(v => v.Kind == ViolationKind.SliceCycle).Should().HaveCount(2);
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
