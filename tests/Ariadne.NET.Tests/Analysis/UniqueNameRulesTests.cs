using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class UniqueNameRulesTests
{
    private static readonly RulePack Pack = PackWith();

    private static RulePack PackWith(params RuleException[] exceptions) => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"] },
            new LayerDefinition { Name = "Web", Id = "TEST-LAYER-WEB", Because = "Test layer Web.", Namespaces = ["**.Web.**"] },
        ],
        Rules =
        [
            new RuleDefinition
            {
                Id = "TEST-UNIQUE",
                Because = "Web types keep names that differ from domain types.",
                Kind = RuleKind.UniqueNames,
                From = new TargetSelector { Layers = ["Web"] },
                Against = new TargetSelector { Layers = ["Domain"] },
                Exceptions = exceptions,
            },
        ],
    };

    [Fact]
    public void ForTypeSharingNameWithAgainstType_ReportsOneViolation()
    {
        var violations = Check(Pack, """
            namespace Shop.Domain { public class Order { } }
            namespace Shop.Web { public class Order { } public class Other { } }
            """);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.NameCollision);
        v.RuleId.Should().Be("TEST-UNIQUE");
        v.Because.Should().Be("Web types keep names that differ from domain types.");
        v.SourceType.Should().Be("Shop.Web.Order");
        v.Target.Should().Be("Shop.Domain.Order");
        v.Location.Should().Be(new SourceLocation("Test.cs", 2, 35));
        v.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void Arity_ResultAndResultOfT_DoNotCollide()
    {
        Check(Pack, """
            namespace Shop.Domain { public class Result { } }
            namespace Shop.Web { public class Result<T> { } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void TypeInBothSets_DoesNotCollideWithItself()
    {
        var pack = new RulePack
        {
            Name = "test",
            Rules =
            [
                new RuleDefinition
                {
                    Id = "TEST-UNIQUE",
                    Because = "Names are unique.",
                    Kind = RuleKind.UniqueNames,
                    From = new TargetSelector { Namespaces = ["Shop.**"] },
                    Against = new TargetSelector { Namespaces = ["Shop.**"] },
                },
            ],
        };

        Check(pack, "namespace Shop.A { public class Order { } } namespace Shop.B { public class Order { } public class Solo { } }")
            .Select(v => (v.SourceType, v.Target))
            .Should().BeEquivalentTo(new[] { ("Shop.A.Order", "Shop.B.Order"), ("Shop.B.Order", "Shop.A.Order") });
    }

    [Fact]
    public void SeveralAgainstMatches_OneViolationPerPair()
    {
        var violations = Check(Pack, """
            namespace Shop.Domain.A { public class Order { } }
            namespace Shop.Domain.B { public class Order { } }
            namespace Shop.Web { public class Order { } }
            """);

        violations.Select(v => v.Target).Should().BeEquivalentTo("Shop.Domain.A.Order", "Shop.Domain.B.Order");
        violations.Should().OnlyContain(v => v.SourceType == "Shop.Web.Order");
    }

    [Fact]
    public void CrossProject_TypesInTwoGraphs_Collide()
    {
        var domain = TestCompilation.Create("namespace Shop.Domain { public class Order { } }");
        var web = TestCompilation.Create("namespace Shop.Web { public class Order { } }");

        Check(Pack, domain, web).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Web.Order");
    }

    [Fact]
    public void SameTypeInTwoTargetFrameworks_ReportedOnce()
    {
        const string source = """
            namespace Shop.Domain { public class Order { } }
            namespace Shop.Web { public class Order { } }
            """;

        Check(Pack, TestCompilation.Create(source), TestCompilation.Create(source)).Should().ContainSingle();
    }

    [Fact]
    public void Exception_NamingForType_Suppresses()
    {
        var pack = PackWith(new RuleException { Types = ["Shop.Web.Order"], Because = "Legacy name." });

        Check(pack, """
            namespace Shop.Domain { public class Order { } }
            namespace Shop.Web { public class Order { } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void NamesDifferByCase_DoNotCollide()
    {
        Check(Pack, """
            namespace Shop.Domain { public class Order { } }
            namespace Shop.Web { public class order { } }
            """).Should().BeEmpty();
    }

    [Fact]
    public void SameFullNameInTwoAssemblies_SelectedByAssembly_Collide()
    {
        var pack = new RulePack
        {
            Name = "test",
            Rules =
            [
                new RuleDefinition
                {
                    Id = "TEST-UNIQUE",
                    Because = "Web keeps its own names.",
                    Kind = RuleKind.UniqueNames,
                    From = new TargetSelector { Assemblies = ["Shop.Web"] },
                    Against = new TargetSelector { Assemblies = ["Shop.Domain"] },
                },
            ],
        };
        const string source = "namespace Shop { public class Order { } }";
        var web = TestCompilation.Create(source).WithAssemblyName("Shop.Web");
        var domain = TestCompilation.Create(source).WithAssemblyName("Shop.Domain");

        Check(pack, domain, web).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Order");
        var violation = Check(pack, web, domain).Should().ContainSingle().Subject;
        violation.SourceType.Should().Be("Shop.Order");

        // Target stays the full name; the message names the assemblies, since the two full names alone read the same.
        violation.Target.Should().Be("Shop.Order");
        violation.Message.Should().Be(
            "Type 'Shop.Order' in assembly 'Shop.Web' is selected by rule 'TEST-UNIQUE' and shares its name with 'Shop.Order' in assembly 'Shop.Domain'");
    }

    [Fact]
    public void NestedType_CollidesByItsOwnName()
    {
        var violations = Check(Pack, """
            namespace Shop.Domain { public class Order { } }
            namespace Shop.Web { public class Page { public class Order { } } }
            """);

        violations.Should().ContainSingle().Which.SourceType.Should().Be("Shop.Web.Page.Order");
    }

    [Fact]
    public void ForTypeCollidesWithAgainstTypeInThirdProject()
    {
        var other = TestCompilation.Create("namespace Shop.Other { public class Unrelated { } }");
        var domain = TestCompilation.Create("namespace Shop.Domain { public class Order { } }");
        var web = TestCompilation.Create("namespace Shop.Web { public class Order { } }");

        var violation = Check(Pack, other, domain, web).Should().ContainSingle().Subject;
        violation.SourceType.Should().Be("Shop.Web.Order");
        violation.Target.Should().Be("Shop.Domain.Order");
    }

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, params string[] sources) =>
        Check(pack, sources.Select(s => TestCompilation.Create(s)).ToArray());

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, params CSharpCompilation[] compilations)
    {
        var graphs = compilations.Select(c => TypeGraphBuilder.Build(c, new HashSet<SyntaxTree>(), CancellationToken.None)).ToList();
        return UniqueNameRules.Check(new LayerMap(pack), pack, graphs);
    }
}
