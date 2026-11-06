using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class RequireRulesTests
{
    private static readonly RulePack TwoLayers = new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"] },
            new LayerDefinition { Name = "Infrastructure", Id = "TEST-LAYER-INFRA", Because = "Test layer Infrastructure.", Namespaces = ["**.Infrastructure.**"] },
        ],
    };

    private static RulePack PackWith(RuleDefinition rule, RulePack? basePack = null) => new()
    {
        Name = "test",
        Layers = (basePack ?? TwoLayers).Layers,
        Rules = [rule],
    };

    private static RuleDefinition Rule(TargetSelector from, RuleRequirement requirement, params RuleException[] exceptions) => new()
    {
        Id = "TEST-REQUIRE",
        Because = "Persistence belongs to Infrastructure.",
        Kind = RuleKind.Require,
        From = from,
        Require = requirement,
        Exceptions = exceptions,
    };

    private static readonly TargetSelector RepositoryTypes = new() { Types = ["**.*Repository"] };

    [Fact]
    public void Layer_TypeInAnotherLayer_ReportsWrongLayer()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Layer = "Infrastructure" }));

        var v = Check("namespace Shop.Domain { public class OrderRepository { } }", pack).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.WrongLayer);
        v.SourceType.Should().Be("Shop.Domain.OrderRepository");
        v.Message.Should().Be("Type 'OrderRepository' is selected by rule 'TEST-REQUIRE' and should be in layer 'Infrastructure' but is in layer 'Domain'");
    }

    [Fact]
    public void Layer_TypeInNoLayer_ReportsWrongLayer()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Layer = "Infrastructure" }));

        var v = Check("namespace Shop.Misc { public class OrderRepository { } }", pack).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.WrongLayer);
        v.Message.Should().Be("Type 'OrderRepository' is selected by rule 'TEST-REQUIRE' and should be in layer 'Infrastructure' but is in no layer");
    }

    [Fact]
    public void Layer_TypeInRequiredLayer_ReportsNothing()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Layer = "Infrastructure" }));

        Check("namespace Shop.Infrastructure { public class OrderRepository { } }", pack).Should().BeEmpty();
    }

    [Fact]
    public void Layer_TypeInTwoLayers_ReportsNoWrongLayer()
    {
        var overlapping = new RulePack
        {
            Name = "test",
            Layers =
            [
                new LayerDefinition { Name = "Domain", Id = "L1", Because = "b", Namespaces = ["**.Domain.**"] },
                new LayerDefinition { Name = "Infrastructure", Id = "L2", Because = "b", Namespaces = ["**.Domain.**"] },
            ],
            Rules = [Rule(RepositoryTypes, new RuleRequirement { Layer = "Infrastructure" })],
        };

        Check("namespace Shop.Domain { public class OrderRepository { } }", overlapping).Should().BeEmpty();
    }

    [Fact]
    public void Kind_Mismatch_ReportsTypeKindMismatch()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Kind = CSharpTypeKind.Class }));

        var v = Check("namespace Shop { public record OrderRepository; }", pack).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.TypeKindMismatch);
        v.Message.Should().Be("Type 'OrderRepository' is selected by rule 'TEST-REQUIRE' and should be a 'Class' but is declared as a 'Record'");
    }

    [Fact]
    public void Accessibility_Internal_ReportsPublicType()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Accessibility = "internal" }));

        var violations = Check("""
            namespace Shop {
                public class OrderRepository { }
                internal class UserRepository { }
            }
            """, pack);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.AccessibilityMismatch);
        v.SourceType.Should().Be("Shop.OrderRepository");
        v.Message.Should().Be("Type 'OrderRepository' is selected by rule 'TEST-REQUIRE' and should be 'internal' but is declared 'public'");
    }

    [Fact]
    public void Accessibility_Public_OrUnknown_RequiresNothing()
    {
        foreach (var accessibility in new[] { "public", "bogus" })
        {
            var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Accessibility = accessibility }));

            Check("namespace Shop { internal class OrderRepository { } }", pack).Should().BeEmpty();
        }
    }

    [Fact]
    public void Implements_NoMatchingInterface_ReportsMissingInterface()
    {
        var pack = PackWith(Rule(new TargetSelector { Types = ["**.OrderRepository"] }, new RuleRequirement { Implements = new TargetSelector { Types = ["**.IRepository"] } }));

        var v = Check("""
            namespace Shop {
                public interface IRepository { }
                public interface IOther { }
                public class OrderRepository : IOther { }
            }
            """, pack).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.MissingInterface);
        v.SourceType.Should().Be("Shop.OrderRepository");
        v.Message.Should().Be("Type 'OrderRepository' is selected by rule 'TEST-REQUIRE' and should implement an interface selected by the rule but does not");
    }

    [Fact]
    public void Implements_InterfaceFromBaseClass_Satisfies()
    {
        var pack = PackWith(Rule(new TargetSelector { Types = ["**.OrderRepository"] }, new RuleRequirement { Implements = new TargetSelector { Types = ["**.IRepository"] } }));

        Check("""
            namespace Shop {
                public interface IRepository { }
                public abstract class Base : IRepository { }
                public class OrderRepository : Base { }
            }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void Exception_ExcludesTheNamedType()
    {
        var pack = PackWith(Rule(
            RepositoryTypes,
            new RuleRequirement { Layer = "Infrastructure" },
            new RuleException { Types = ["**.OrderRepository"], Because = "Known." }));

        var violations = Check("""
            namespace Shop.Domain { public class OrderRepository { } public class UserRepository { } }
            """, pack);

        violations.Should().ContainSingle().Which.SourceType.Should().Be("Shop.Domain.UserRepository");
    }

    [Fact]
    public void ForSelectingNothing_ReportsNothing()
    {
        var pack = PackWith(Rule(new TargetSelector { Types = ["**.Nothing"] }, new RuleRequirement { Layer = "Infrastructure" }));

        Check("namespace Shop.Domain { public class Order { } }", pack).Should().BeEmpty();
    }

    [Fact]
    public void EnumSelectedByFor_OnlyLayerApplies()
    {
        var pack = PackWith(Rule(
            new TargetSelector { Types = ["**.Status"] },
            new RuleRequirement
            {
                Layer = "Infrastructure",
                Kind = CSharpTypeKind.Interface,
                Accessibility = "internal",
                Implements = new TargetSelector { Types = ["**.IThing"] },
            }));

        var violations = Check("namespace Shop.Domain { public enum Status { A } public delegate void Status2(); }", pack);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.WrongLayer);
        v.SourceType.Should().Be("Shop.Domain.Status");
    }

    [Fact]
    public void Violation_CarriesRuleIdBecauseAndLocation()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Layer = "Infrastructure" }));

        var v = Check("namespace Shop.Domain { public class OrderRepository { } }", pack).Should().ContainSingle().Subject;

        v.RuleId.Should().Be("TEST-REQUIRE");
        v.Because.Should().Be("Persistence belongs to Infrastructure.");
        v.Location.Should().Be(new SourceLocation("Test.cs", 1, 38));
    }

    [Fact]
    public void OneViolationPerFailedRequirement()
    {
        var pack = PackWith(Rule(RepositoryTypes, new RuleRequirement { Layer = "Infrastructure", Kind = CSharpTypeKind.Interface, Accessibility = "internal" }));

        var kinds = Check("namespace Shop.Domain { public class OrderRepository { } }", pack).Select(v => v.Kind);

        kinds.Should().BeEquivalentTo([ViolationKind.WrongLayer, ViolationKind.TypeKindMismatch, ViolationKind.AccessibilityMismatch]);
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source, RulePack pack)
    {
        var graph = TypeGraphBuilder.Build(TestCompilation.Create(source), new HashSet<SyntaxTree>(), CancellationToken.None);
        return RequireRules.Check(graph, new LayerMap(pack), pack);
    }
}
