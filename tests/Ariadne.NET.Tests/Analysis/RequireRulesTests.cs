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

    private static RulePack ConditionPack(TargetSelector from, RuleRequirement requirement, bool allowEmpty = false) => new()
    {
        Name = "test",
        Rules =
        [
            new RuleDefinition
            {
                Id = "R",
                Because = "Because R.",
                Kind = RuleKind.Require,
                From = from,
                Require = requirement,
                AllowEmpty = allowEmpty,
            },
        ],
    };

    private static readonly TargetSelector InN = new() { Namespaces = ["N.**"] };

    [Fact]
    public void Is_reports_one_violation_per_failed_key()
    {
        var pack = ConditionPack(new TargetSelector { Namespaces = ["N"] }, new RuleRequirement { Is = new TargetSelector { Sealed = true, Names = ["*Service"] } });

        var violations = Check("namespace N { public class OrderHandler { } }", pack);

        violations.Should().HaveCount(2).And.OnlyContain(v => v.Kind == ViolationKind.RequirementNotMet && string.Equals(v.RuleId, "R", StringComparison.Ordinal) && string.Equals(v.SourceType, "N.OrderHandler", StringComparison.Ordinal));
        violations.Select(v => v.Message).Should().BeEquivalentTo(
        [
            "Type 'OrderHandler' is selected by rule 'R' and should have a name matching '*Service' but is named 'OrderHandler'",
            "Type 'OrderHandler' is selected by rule 'R' and should be sealed but is not",
        ]);
    }

    [Fact]
    public void IsNot_reports_one_violation_per_matched_key()
    {
        var pack = ConditionPack(InN, new RuleRequirement { IsNot = new TargetSelector { Names = ["*Impl"], Abstract = true } });

        var messages = Check("namespace N { public abstract class OrderImpl { } }", pack).Select(v => v.Message).ToList();

        messages.Should().HaveCount(2);
        messages.Should().ContainMatch("*should not have a name matching '*Impl' but is named 'OrderImpl'");
        messages.Should().ContainMatch("*should not be abstract but is");
    }

    [Fact]
    public void Old_keys_still_skip_enums_new_keys_do_not()
    {
        const string Source = "namespace N { public enum E { A } }";

        Check(Source, ConditionPack(InN, new RuleRequirement { Kind = CSharpTypeKind.Class })).Should().BeEmpty();

        var v = Check(Source, ConditionPack(InN, new RuleRequirement { Is = new TargetSelector { Kinds = [CSharpTypeKind.Class] } }))
            .Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.RequirementNotMet);
        v.Message.Should().Be("Type 'E' is selected by rule 'R' and should be a 'Class' but is a 'Enum'");
    }

    [Fact]
    public void NotExist_idiom_flags_every_selected_type()
    {
        var pack = ConditionPack(new TargetSelector { Namespaces = ["N.Legacy.**"] }, new RuleRequirement { IsNot = new TargetSelector { Types = ["**"] } }, allowEmpty: true);

        Check("namespace N.Legacy { public class A { } public class B { } }", pack)
            .Select(v => v.SourceType).Should().BeEquivalentTo(["N.Legacy.A", "N.Legacy.B"]);

        const string Empty = "namespace N.Other { public class C { } }";
        Check(Empty, pack).Should().BeEmpty();
        var compilation = TestCompilation.Create(Empty);
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        VacuityRules.Check(pack, new LayerMap(pack), [graph], [], [new AnalyzedProject("Shop", null, compilation)]).Should().BeEmpty();
    }

    [Fact]
    public void Selector_keys_work_as_conditions()
    {
        var inDomain = ConditionPack(InN, new RuleRequirement { Is = new TargetSelector { Namespaces = ["N.Domain.**"] } });
        Check("namespace N.Domain { public class A { } } namespace N.Api { public class B { } }", inDomain)
            .Should().ContainSingle().Which.Message.Should().Be("Type 'B' is selected by rule 'R' and should be in a namespace matching 'N.Domain.**' but is in namespace 'N.Api'");

        var obsolete = ConditionPack(InN, new RuleRequirement { Is = new TargetSelector { Attributes = ["**.ObsoleteAttribute"] } });
        Check("namespace N { [System.Obsolete] public class A { } public class B { } }", obsolete)
            .Should().ContainSingle().Which.Message.Should().Be("Type 'B' is selected by rule 'R' and should have an attribute matching '**.ObsoleteAttribute' but does not");

        var disposable = ConditionPack(InN, new RuleRequirement { IsNot = new TargetSelector { Interfaces = ["**.IDisposable"] } });
        Check("namespace N { public class A : System.IDisposable { public void Dispose() { } } public class B { } }", disposable)
            .Should().ContainSingle().Which.Message.Should().Be("Type 'A' is selected by rule 'R' and should not implement an interface matching '**.IDisposable' but does");
    }

    [Fact]
    public void Several_patterns_and_kinds_are_listed_with_or()
    {
        var pack = ConditionPack(InN, new RuleRequirement
        {
            Is = new TargetSelector { Names = ["*Service", "*Handler"], Kinds = [CSharpTypeKind.Record, CSharpTypeKind.Struct], Accessibility = ["internal"] },
        });

        Check("namespace N { public class Order { } }", pack).Select(v => v.Message).Should().BeEquivalentTo(
        [
            "Type 'Order' is selected by rule 'R' and should have a name matching '*Service' or '*Handler' but is named 'Order'",
            "Type 'Order' is selected by rule 'R' and should be a 'Record' or 'Struct' but is a 'Class'",
            "Type 'Order' is selected by rule 'R' and should be 'internal' but is 'public'",
        ]);
    }

    [Fact]
    public void Boolean_false_reads_as_the_opposite_condition()
    {
        var pack = ConditionPack(InN, new RuleRequirement { Is = new TargetSelector { Sealed = false, Static = false } });

        Check("namespace N { public static class Helpers { } }", pack).Select(v => v.Message).Should().BeEquivalentTo(
        [
            "Type 'Helpers' is selected by rule 'R' and should not be sealed but is",
            "Type 'Helpers' is selected by rule 'R' and should not be static but is",
        ]);
    }

    [Fact]
    public void RequirementNotMet_is_13() => ((int)ViolationKind.RequirementNotMet).Should().Be(13);

    private static IReadOnlyList<ArchitectureViolation> Check(string source, RulePack pack)
    {
        var graph = TypeGraphBuilder.Build(TestCompilation.Create(source), new HashSet<SyntaxTree>(), CancellationToken.None);
        return RequireRules.Check(graph, new LayerMap(pack), pack);
    }
}
