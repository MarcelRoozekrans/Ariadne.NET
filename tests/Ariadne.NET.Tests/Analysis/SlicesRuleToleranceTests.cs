using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

/// <summary>
/// A slices rule leaves from, to and against empty. Every checker that walks the pack's rules must pass it by, so adding
/// one changes nothing the other rule kinds report.
/// </summary>
public class SlicesRuleToleranceTests
{
    private const string Source = """
        namespace Shop.Orders.Domain { public sealed class Order { } public class Invoice { } }
        namespace Shop.Orders.Web
        {
            public class Page
            {
                public Shop.Orders.Domain.Order O = null!;
                public void Run() => System.Console.WriteLine(O.GetHashCode());
            }

            public class Invoice { }
        }
        """;

    private static readonly LayerDefinition[] Layers =
    [
        new() { Name = "Domain", Id = "L-DOMAIN", Because = "Domain.", Namespaces = ["**.Domain.**"] },
        new() { Name = "Web", Id = "L-WEB", Because = "Web.", Namespaces = ["**.Web.**"] },
        new() { Name = "Empty", Id = "L-EMPTY", Because = "Empty.", Namespaces = ["**.Nothing.**"] },
    ];

    private static readonly RuleDefinition[] OtherRules =
    [
        new()
        {
            Id = "R-FORBID",
            Because = "Web does not use the domain.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Web"] },
            To = new TargetSelector { Layers = ["Domain"] },
        },
        new()
        {
            Id = "R-MEMBERS",
            Because = "No console.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Web"] },
            To = new TargetSelector { Members = ["System.Console.WriteLine"] },
        },
        new()
        {
            Id = "R-ALLOWONLY",
            Because = "Only web uses the domain.",
            Kind = RuleKind.AllowOnly,
            From = new TargetSelector { Namespaces = ["Shop.Nowhere.**"] },
            To = new TargetSelector { Layers = ["Domain"] },
        },
        new()
        {
            Id = "R-REQUIRE",
            Because = "Domain types are sealed.",
            Kind = RuleKind.Require,
            From = new TargetSelector { Layers = ["Domain"] },
            Require = new RuleRequirement { Is = new TargetSelector { Sealed = true } },
        },
        new()
        {
            Id = "R-UNIQUE",
            Because = "Web names differ from domain names.",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Web"] },
            Against = new TargetSelector { Layers = ["Domain"] },
        },
        new()
        {
            Id = "R-BANNED",
            Because = "No Newtonsoft.",
            Kind = RuleKind.BannedPackages,
            From = new TargetSelector(),
            Packages = ["Newtonsoft.Json"],
        },
        new()
        {
            Id = "R-VACUOUS",
            Because = "Selects nothing.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Namespaces = ["Shop.Nowhere.**"] },
            To = new TargetSelector { Layers = ["Domain"] },
        },
    ];

    // One slice only, Orders, so the rule finds nothing once slice rules are checked either.
    private static readonly RuleDefinition SlicesRule = new()
    {
        Id = "R-SLICES",
        Because = "Modules stay apart.",
        Kind = RuleKind.Slices,
        From = new TargetSelector(),
        Slices = new SliceDefinition
        {
            Matching = ["Shop.(*).**"],
            Independent = true,
            Allow = new TargetSelector { Layers = ["Domain"] },
        },
        Exceptions = [new RuleException { Because = "Legacy.", Namespaces = ["Shop.Legacy.**"] }],
    };

    private static readonly RulePack Without = new() { Name = "test", Layers = Layers, Rules = OtherRules };

    private static readonly RulePack With = new() { Name = "test", Layers = Layers, Rules = [.. OtherRules, SlicesRule] };

    [Fact]
    public void Slices_rule_passes_every_other_checker_untouched()
    {
        var compilation = TestCompilation.Create(Source);
        var project = new AnalyzedProject("Shop", null, compilation)
        {
            Packages = [new ResolvedPackage("Newtonsoft.Json", "13.0.3", true, ["Newtonsoft.Json"])],
        };

        Run(With, compilation, project).Should().BeEquivalentTo(Run(Without, compilation, project), options => options.WithStrictOrdering());
    }

    [Fact]
    public void The_other_rules_report_something_to_compare()
    {
        var compilation = TestCompilation.Create(Source);
        var project = new AnalyzedProject("Shop", null, compilation)
        {
            Packages = [new ResolvedPackage("Newtonsoft.Json", "13.0.3", true, ["Newtonsoft.Json"])],
        };

        var results = Run(Without, compilation, project);

        results.Forbid.Should().NotBeEmpty();
        results.Require.Should().NotBeEmpty();
        results.Unique.Should().NotBeEmpty();
        results.Banned.Should().NotBeEmpty();
        results.Vacuity.Should().NotBeEmpty();
        results.MemberFilterEmpty.Should().BeFalse();
        results.Probe.Should().Equal("Domain");
    }

    private static Results Run(RulePack pack, CSharpCompilation compilation, AnalyzedProject project)
    {
        var layers = new LayerMap(pack);
        var filter = MemberNameFilter.From(pack);
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), filter, CancellationToken.None);
        return new Results(
            ForbidRules.Check(graph, layers, pack),
            AllowOnlyRules.Check(graph, layers, pack),
            RequireRules.Check(graph, layers, pack),
            UniqueNameRules.Check(layers, pack, [graph]),
            BannedPackageRules.Compile(pack).Check(project),
            VacuityRules.Check(pack, layers, [graph], [RoleRules.Match(graph, layers, pack)], [project]),
            filter.IsEmpty,
            filter.CouldMatch("WriteLine"),
            LayerProbe.LayersOf(pack, "Shop", "Shop.Orders.Domain"),
            new ArchitectureValidator().Validate(pack, [project]).Violations);
    }

    private sealed record Results(
        IReadOnlyList<ArchitectureViolation> Forbid,
        IReadOnlyList<ArchitectureViolation> AllowOnly,
        IReadOnlyList<ArchitectureViolation> Require,
        IReadOnlyList<ArchitectureViolation> Unique,
        IReadOnlyList<ArchitectureViolation> Banned,
        IReadOnlyList<ArchitectureViolation> Vacuity,
        bool MemberFilterEmpty,
        bool MemberFilterMatchesWriteLine,
        IReadOnlyList<string> Probe,
        IReadOnlyList<ArchitectureViolation> Validator);
}
