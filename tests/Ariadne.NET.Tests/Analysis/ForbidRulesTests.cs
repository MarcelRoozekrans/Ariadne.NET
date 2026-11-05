using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class ForbidRulesTests
{
    private static RulePack PackWith(RuleDefinition rule) => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"] },
        ],
        Rules = [rule],
    };

    private static RuleDefinition Rule(TargetSelector from, TargetSelector to, params RuleException[] exceptions) => new()
    {
        Id = "TEST-FORBID",
        Because = "Domain stays free of web code.",
        Kind = RuleKind.Forbid,
        From = from,
        To = to,
        Exceptions = exceptions,
    };

    [Fact]
    public void ForbidNamespace_FromLayer_Fires()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Namespaces = ["Web.**"] }));

        var violations = Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { public Web.Http.Client? C; } }
            """, pack);

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.ForbiddenDependency);
        v.SourceType.Should().Be("Shop.Domain.Order");
        v.Target.Should().Be("Web.Http.Client");
        v.Location.Should().Be(new SourceLocation("Test.cs", 2, 62));
        v.ReferenceCount.Should().Be(1);
        v.Message.Should().Be("Rule 'TEST-FORBID' forbids this dependency: Shop.Domain.Order references Web.Http.Client");
    }

    [Fact]
    public void ForbidAssembly_Fires()
    {
        var library = TestCompilation.Create("namespace Other { public class Ext { } }").WithAssemblyName("Shop.Lib");
        var compilation = TestCompilation.Create(
            [library.ToMetadataReference()],
            ("Test.cs", "namespace Shop.Domain { public class Order { public Other.Ext? E; } }"));
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Assemblies = [library.AssemblyName!] }));

        Check(compilation, pack).Should().ContainSingle().Which.Target.Should().Be("Other.Ext");
    }

    [Fact]
    public void ForbidWithinSameLayer_Fires()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Layers = ["Domain"] }));

        Check("""
            namespace Shop.Domain { public class A { } public class B { public A? A; } }
            """, pack).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Domain.B");
    }

    [Fact]
    public void ExceptedSource_NoViolation()
    {
        var pack = PackWith(Rule(
            new TargetSelector { Layers = ["Domain"] },
            new TargetSelector { Namespaces = ["Web.**"] },
            new RuleException { Types = ["**.Order"], Because = "Order is a known bridge." }));

        Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { public Web.Http.Client? C; } }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void NotMatchingFrom_NoViolation()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Namespaces = ["Web.**"] }));

        Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Misc { public class Thing { public Web.Http.Client? C; } }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void ThirtyReferences_OneViolationWithCount30()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Namespaces = ["Web.**"] }));
        var fields = string.Concat(Enumerable.Range(0, 30).Select(i => $"public Web.Http.Client? F{i}; "));

        var v = Check($$"""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { {{fields}} } }
            """, pack).Should().ContainSingle().Subject;

        v.ReferenceCount.Should().Be(30);
        v.Message.Should().EndWith("Web.Http.Client 30 times");
        v.Location.Should().Be(new SourceLocation("Test.cs", 2, 62));
    }

    [Fact]
    public void Violation_CarriesRuleIdAndBecause()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Namespaces = ["Web.**"] }));

        var v = Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { public Web.Http.Client? C; } }
            """, pack).Should().ContainSingle().Subject;

        v.RuleId.Should().Be("TEST-FORBID");
        v.Because.Should().Be("Domain stays free of web code.");
    }

    [Fact]
    public void EmptyFrom_FiresNothing()
    {
        var pack = PackWith(Rule(new TargetSelector(), new TargetSelector { Namespaces = ["Web.**"] }));

        Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { public Web.Http.Client? C; } }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void EmptyTo_FiresNothing()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector()));

        Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { public Web.Http.Client? C; } }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void AllowOnlyRule_IsIgnored()
    {
        var rule = Rule(new TargetSelector { Layers = ["Domain"] }, new TargetSelector { Namespaces = ["Web.**"] }) ;
        var pack = PackWith(new RuleDefinition { Id = rule.Id, Because = rule.Because, Kind = RuleKind.AllowOnly, From = rule.From, To = rule.To });

        Check("""
            namespace Web.Http { public class Client { } }
            namespace Shop.Domain { public class Order { public Web.Http.Client? C; } }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void TargetMixingLayersAndNamespaces_Throws()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Domain"], Namespaces = ["Web.**"] }, new TargetSelector { Namespaces = ["Web.**"] }));

        var act = () => Check("namespace Shop.Domain { public class Order { } }", pack);

        act.Should().Throw<ArgumentException>();
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source, RulePack pack) =>
        Check(TestCompilation.Create(source), pack);

    private static IReadOnlyList<ArchitectureViolation> Check(CSharpCompilation compilation, RulePack pack)
    {
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        return ForbidRules.Check(graph, new LayerMap(pack), pack);
    }
}
