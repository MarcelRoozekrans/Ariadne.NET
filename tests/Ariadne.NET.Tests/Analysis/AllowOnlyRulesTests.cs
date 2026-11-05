using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class AllowOnlyRulesTests
{
    private static RulePack PackWith(RuleDefinition rule) => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Hosts", Id = "TEST-LAYER-HOSTS", Because = "Test layer Hosts.", Namespaces = ["**.Hosts.**"] },
        ],
        Rules = [rule],
    };

    private static RuleDefinition Rule(TargetSelector from, TargetSelector to, params RuleException[] exceptions) => new()
    {
        Id = "TEST-ALLOWONLY",
        Because = "Only hosts touch the runner.",
        Kind = RuleKind.AllowOnly,
        From = from,
        To = to,
        Exceptions = exceptions,
    };

    private static readonly TargetSelector Runner = new() { Types = ["**.ISubagentRunner"] };

    private const string RunnerSource = "namespace Core.Agents { public interface ISubagentRunner { } }\n";

    [Fact]
    public void AllowedSource_NoViolation()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Hosts"] }, Runner));

        Check(RunnerSource + "namespace App.Hosts { public class Host { public Core.Agents.ISubagentRunner? R; } }", pack)
            .Should().BeEmpty();
    }

    [Fact]
    public void OtherSource_Fires()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Hosts"] }, Runner));

        var v = Check(RunnerSource + "namespace App.Misc { public class Thing { public Core.Agents.ISubagentRunner? R; } }", pack)
            .Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.DependencyNotPermitted);
        v.RuleId.Should().Be("TEST-ALLOWONLY");
        v.Because.Should().Be("Only hosts touch the runner.");
        v.SourceType.Should().Be("App.Misc.Thing");
        v.Target.Should().Be("Core.Agents.ISubagentRunner");
        v.Message.Should().Be("Rule 'TEST-ALLOWONLY' allows only selected types to depend on Core.Agents.ISubagentRunner: App.Misc.Thing references Core.Agents.ISubagentRunner");
    }

    [Fact]
    public void TargetTypesReferencingEachOther_NoViolation()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Hosts"] }, new TargetSelector { Namespaces = ["Core.Agents"] }));

        Check("""
            namespace Core.Agents { public class A { } public class B { public A? A; } }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void TypeTargetWithTwoExceptions_OnlyUnexceptedFires()
    {
        var pack = PackWith(Rule(
            new TargetSelector { Layers = ["Hosts"] },
            Runner,
            new RuleException { Types = ["**.Orchestrator"], Because = "Orchestrator drives runners." },
            new RuleException { Types = ["**.Scheduler"], Because = "Scheduler drives runners." }));

        var violations = Check(RunnerSource + """
            namespace App.Misc
            {
                public class Orchestrator { public Core.Agents.ISubagentRunner? R; }
                public class Scheduler { public Core.Agents.ISubagentRunner? R; }
                public class Stranger { public Core.Agents.ISubagentRunner? R; }
            }
            """, pack);

        violations.Should().ContainSingle().Which.SourceType.Should().Be("App.Misc.Stranger");
    }

    [Fact]
    public void AssemblyTarget_Fires()
    {
        var library = TestCompilation.Create("namespace Other { public class Ext { } }").WithAssemblyName("Shop.Lib");
        var compilation = TestCompilation.Create(
            [library.ToMetadataReference()],
            ("Test.cs", "namespace App.Misc { public class Thing { public Other.Ext? E; } }"));
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Hosts"] }, new TargetSelector { Assemblies = [library.AssemblyName!] }));

        Check(compilation, pack).Should().ContainSingle().Which.Target.Should().Be("Other.Ext");
    }

    [Theory]
    [InlineData("System.Collections.Generic.List<Core.Agents.ISubagentRunner>")]
    [InlineData("System.Func<Core.Agents.ISubagentRunner>")]
    public void ConstructedGenericOfTarget_Fires(string fieldType)
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Hosts"] }, Runner));

        var v = Check(RunnerSource + $"namespace App.Misc {{ public class Thing {{ public {fieldType}? R; }} }}", pack)
            .Should().ContainSingle().Subject;

        v.SourceType.Should().Be("App.Misc.Thing");
        v.Target.Should().Be("Core.Agents.ISubagentRunner");
    }

    [Fact]
    public void SeveralReferencesFromOneSource_OneViolationWithCount()
    {
        var pack = PackWith(Rule(new TargetSelector { Layers = ["Hosts"] }, Runner));

        var v = Check(RunnerSource + "namespace App.Misc { public class Thing { public Core.Agents.ISubagentRunner? A; public Core.Agents.ISubagentRunner? B; public Core.Agents.ISubagentRunner? C; } }", pack)
            .Should().ContainSingle().Subject;

        v.ReferenceCount.Should().Be(3);
        v.Message.Should().EndWith("references Core.Agents.ISubagentRunner 3 times");
    }

    [Fact]
    public void EmptyFrom_EveryNonTargetSourceViolates()
    {
        var pack = PackWith(Rule(new TargetSelector(), Runner));

        Check(RunnerSource + "namespace App.Hosts { public class Host { public Core.Agents.ISubagentRunner? R; } }", pack)
            .Should().ContainSingle().Which.SourceType.Should().Be("App.Hosts.Host");
    }

    [Fact]
    public void ForbidRule_IsIgnored()
    {
        var rule = Rule(new TargetSelector { Layers = ["Hosts"] }, Runner);
        var pack = PackWith(new RuleDefinition { Id = rule.Id, Because = rule.Because, Kind = RuleKind.Forbid, From = new TargetSelector { Layers = ["Hosts"] }, To = Runner });

        Check(RunnerSource + "namespace App.Misc { public class Thing { public Core.Agents.ISubagentRunner? R; } }", pack)
            .Should().BeEmpty();
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source, RulePack pack) =>
        Check(TestCompilation.Create(source), pack);

    private static IReadOnlyList<ArchitectureViolation> Check(CSharpCompilation compilation, RulePack pack)
    {
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        return AllowOnlyRules.Check(graph, new LayerMap(pack), pack);
    }
}
