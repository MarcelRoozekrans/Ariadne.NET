using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class MemberRulesTests
{
    private const string DataTypes = """
        namespace Shop.Data
        {
            public abstract class DbCommand
            {
                public abstract int ExecuteReader();
                public virtual string? Text { get; set; }
            }
            public class SqlCommand : DbCommand
            {
                public SqlCommand() { }
                public override int ExecuteReader() => 0;
                public override string? Text { get; set; }
                public int Timeout;
            }
            public class Cache<T> { public void Put(T item) { } }
            public interface IClock { int Now(); }
            public abstract class Grid { public abstract int this[int i] { get; } }
            public static class Extras { public static string Describe(this DbCommand c) => ""; }
        }
        namespace Other { public class SqlCommand { public int Timeout; } }
        """;

    private static RulePack PackWith(RuleDefinition rule) => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Infrastructure", Id = "TEST-LAYER-INFRA", Because = "Infra.", Namespaces = ["**.Infrastructure.**"] },
        ],
        Rules = [rule],
    };

    private static RuleDefinition Forbid(TargetSelector to, params RuleException[] exceptions) => new()
    {
        Id = "TEST-MEMBERS",
        Because = "SQL stays in infrastructure.",
        Kind = RuleKind.Forbid,
        From = new TargetSelector { Namespaces = ["**.Domain.**"] },
        To = to,
        Exceptions = exceptions,
    };

    private static RuleDefinition AllowOnly(TargetSelector to, TargetSelector? from = null, params RuleException[] exceptions) => new()
    {
        Id = "TEST-MEMBERS",
        Because = "SQL stays in infrastructure.",
        Kind = RuleKind.AllowOnly,
        From = from ?? new TargetSelector { Layers = ["Infrastructure"] },
        To = to,
        Exceptions = exceptions,
    };

    private static TargetSelector Members(params string[] members) => new() { Members = members };

    [Fact]
    public void Forbid_CallThroughSubclass_MatchesBaseMemberPattern()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.DbCommand.ExecuteReader")));

        var v = Check("""
            namespace Shop.Domain { public class Order { public int Run(Data.SqlCommand c) => c.ExecuteReader(); } }
            """, pack).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.ForbiddenDependency);
        v.SourceType.Should().Be("Shop.Domain.Order");
        v.Target.Should().Be("Shop.Data.SqlCommand.ExecuteReader");
        v.Location.Should().Be(new SourceLocation("Test.cs", 1, 85));
        v.ReferenceCount.Should().Be(1);
        v.Message.Should().Be("Rule 'TEST-MEMBERS' forbids this dependency: Shop.Domain.Order calls Shop.Data.SqlCommand.ExecuteReader (which overrides Shop.Data.DbCommand.ExecuteReader)");
    }

    [Fact]
    public void Forbid_TypeofOnly_NoViolation()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.SqlCommand.*")));

        Check("""
            namespace Shop.Domain
            {
                public class Order
                {
                    public System.Type T => typeof(Data.SqlCommand);
                    public Data.SqlCommand? Field;
                    public void M(Data.SqlCommand? c) { }
                }
            }
            """, pack).Should().BeEmpty();
    }

    [Fact]
    public void Forbid_PropertyAccess_Reported()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.SqlCommand.Text")));

        var v = Check("""
            namespace Shop.Domain { public class Order { public string? Read(Data.SqlCommand c) => c.Text; } }
            """, pack).Should().ContainSingle().Subject;

        v.Target.Should().Be("Shop.Data.SqlCommand.Text");
        v.Message.Should().Be("Rule 'TEST-MEMBERS' forbids this dependency: Shop.Domain.Order uses Shop.Data.SqlCommand.Text");
    }

    [Fact]
    public void Forbid_ConstructorPattern_Reported()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.SqlCommand.new")));

        Check("""
            namespace Shop.Domain { public class Order { public object Make() => new Data.SqlCommand(); } }
            """, pack).Should().ContainSingle().Which.Target.Should().Be("Shop.Data.SqlCommand.new");
    }

    [Fact]
    public void AllowOnly_OutsideAllowedLayer_ReportsDependencyNotPermitted()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Data.SqlCommand.*")));

        var v = Check("""
            namespace Shop.Domain { public class Order { public int Run(Data.SqlCommand c) => c.Timeout; } }
            """, pack, ViolationKind.DependencyNotPermitted).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.DependencyNotPermitted);
        v.SourceType.Should().Be("Shop.Domain.Order");
        v.Target.Should().Be("Shop.Data.SqlCommand.Timeout");
        v.Message.Should().Be("Rule 'TEST-MEMBERS' allows only selected types to depend on Shop.Data.SqlCommand.Timeout: Shop.Domain.Order uses Shop.Data.SqlCommand.Timeout");
    }

    [Fact]
    public void AllowOnly_InsideAllowedLayer_NoViolation()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Data.SqlCommand.*")));

        Check("""
            namespace Shop.Infrastructure { public class Repo { public int Run(Data.SqlCommand c) => c.Timeout + c.ExecuteReader(); } }
            """, pack, ViolationKind.DependencyNotPermitted).Should().BeEmpty();
    }

    [Fact]
    public void MembersWithNamespaces_AndsOnContainingType()
    {
        var pack = PackWith(Forbid(new TargetSelector { Namespaces = ["Shop.**"], Members = ["**.SqlCommand.Timeout"] }));

        var v = Check("""
            namespace Shop.Domain { public class Order { public int Run(Data.SqlCommand c, Other.SqlCommand o) => c.Timeout + o.Timeout; } }
            """, pack).Should().ContainSingle().Subject;

        v.Target.Should().Be("Shop.Data.SqlCommand.Timeout");
    }

    [Fact]
    public void GenericPattern_CoversEveryInstantiation()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.Cache.Put")));

        var v = Check("""
            namespace Shop.Domain
            {
                public class Order
                {
                    public void Run(Data.Cache<int> a, Data.Cache<string> b) { a.Put(1); b.Put("x"); }
                }
            }
            """, pack).Should().ContainSingle().Subject;

        v.Target.Should().Be("Shop.Data.Cache.Put");
        v.ReferenceCount.Should().Be(2);
    }

    [Fact]
    public void Exception_NamingType_Suppresses()
    {
        var pack = PackWith(Forbid(
            Members("Shop.Data.SqlCommand.Timeout"),
            new RuleException { Types = ["**.Order"], Because = "Order is a known bridge." }));

        Check("""
            namespace Shop.Domain
            {
                public class Order { public int Run(Data.SqlCommand c) => c.Timeout; }
                public class Invoice { public int Run(Data.SqlCommand c) => c.Timeout; }
            }
            """, pack).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Domain.Invoice");
    }

    [Fact]
    public void CallCountedOncePerRuleSourceAndTarget()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.SqlCommand.*")));

        var violations = Check("""
            namespace Shop.Domain
            {
                public class Order
                {
                    public int Run(Data.SqlCommand c) => c.Timeout + c.Timeout + c.ExecuteReader();
                }
            }
            """, pack);

        violations.Should().HaveCount(2);
        var timeout = violations.Single(v => string.Equals(v.Target, "Shop.Data.SqlCommand.Timeout", StringComparison.Ordinal));
        timeout.ReferenceCount.Should().Be(2);
        timeout.Message.Should().EndWith(" 2 times");
        timeout.Location.Should().Be(new SourceLocation("Test.cs", 5, 48));
    }

    [Fact]
    public void Forbid_ImplementedInterfaceMember_NamesImplements()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.IClock.Now")));

        Check("""
            namespace Shop.Domain
            {
                public class Clock : Data.IClock { public int Now() => 1; }
                public class Order { public int Run(Clock c) => c.Now(); }
            }
            """, pack).Should().ContainSingle().Which.Message
            .Should().Be("Rule 'TEST-MEMBERS' forbids this dependency: Shop.Domain.Order calls Shop.Domain.Clock.Now (which implements Shop.Data.IClock.Now)");
    }

    [Fact]
    public void AllowOnly_TypeUsingItsOwnMember_NoViolation()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Domain.Order.Total")));

        Check("""
            namespace Shop.Domain { public class Order { public int Total; public int Run() => Total; } }
            """, pack, ViolationKind.DependencyNotPermitted).Should().BeEmpty();
    }

    [Fact]
    public void AllowOnly_NestedTypeUsingOuterMember_NoViolation()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Domain.Order.Total")));

        Check("""
            namespace Shop.Domain { public class Order { public int Total; public class Line { public int Run(Order o) => o.Total; } } }
            """, pack, ViolationKind.DependencyNotPermitted).Should().BeEmpty();
    }

    [Fact]
    public void AllowOnly_ExtensionBlockMemberUsedByItsOwnStaticClass_NoViolation_UsedElsewhere_Reported()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Domain.CommandExtensions.Persist")));

        // The walker attributes the code inside an extension block to the static class around it, so that class uses its own member.
        var violations = Check("""
            namespace Shop.Domain
            {
                public static class CommandExtensions
                {
                    extension(Shop.Data.SqlCommand command)
                    {
                        public void Persist() { }
                        public void PersistTwice() { command.Persist(); command.Persist(); }
                    }
                    public static void Save(Shop.Data.SqlCommand command) => command.Persist();
                }
                public class Order { public void Place(Shop.Data.SqlCommand command) => command.Persist(); }
            }
            """, pack, ViolationKind.DependencyNotPermitted);

        var v = violations.Should().ContainSingle().Subject;
        v.SourceType.Should().Be("Shop.Domain.Order");
        v.Target.Should().Be("Shop.Domain.CommandExtensions.Persist");
    }

    [Fact]
    public void AllowOnly_DerivedTypeCallingInheritedMember_Reported()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Data.DbCommand.ExecuteReader")));

        var v = Check("""
            namespace Shop.Domain { public class Fast : Data.SqlCommand { public int Run() => ExecuteReader(); } }
            """, pack, ViolationKind.DependencyNotPermitted).Should().ContainSingle().Subject;

        v.SourceType.Should().Be("Shop.Domain.Fast");
        v.Target.Should().Be("Shop.Data.SqlCommand.ExecuteReader");
    }

    [Fact]
    public void AllowOnly_ExceptionNamingType_Suppresses()
    {
        var pack = PackWith(AllowOnly(
            Members("Shop.Data.SqlCommand.Timeout"),
            null,
            new RuleException { Types = ["**.Order"], Because = "Order is a known bridge." }));

        Check("""
            namespace Shop.Domain
            {
                public class Order { public int Run(Data.SqlCommand c) => c.Timeout; }
                public class Invoice { public int Run(Data.SqlCommand c) => c.Timeout; }
            }
            """, pack, ViolationKind.DependencyNotPermitted).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Domain.Invoice");
    }

    [Fact]
    public void AllowOnly_SourceSelectedByFrom_NoViolation()
    {
        var pack = PackWith(AllowOnly(Members("Shop.Data.SqlCommand.Timeout"), new TargetSelector { Types = ["**.Order"] }));

        Check("""
            namespace Shop.Domain
            {
                public class Order { public int Run(Data.SqlCommand c) => c.Timeout; }
                public class Invoice { public int Run(Data.SqlCommand c) => c.Timeout; }
            }
            """, pack, ViolationKind.DependencyNotPermitted).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Domain.Invoice");
    }

    [Fact]
    public void Forbid_IndexerUse_Reported()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.Grid.Item")));

        Check("""
            namespace Shop.Domain { public class Order { public int Run(Data.Grid g) => g[1]; } }
            """, pack).Should().ContainSingle().Which.Target.Should().Be("Shop.Data.Grid.Item");
    }

    [Fact]
    public void Forbid_ExtensionMethodCall_Reported()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.Extras.Describe")));

        Check("""
            using Shop.Data;
            namespace Shop.Domain { public class Order { public string Run(Data.SqlCommand c) => c.Describe(); } }
            """, pack).Should().ContainSingle().Which.Target.Should().Be("Shop.Data.Extras.Describe");
    }

    [Fact]
    public void Forbid_TargetTypedNew_Reported()
    {
        var pack = PackWith(Forbid(Members("Shop.Data.SqlCommand.new")));

        Check("""
            namespace Shop.Domain { public class Order { public Data.SqlCommand Make() => new(); } }
            """, pack).Should().ContainSingle().Which.Target.Should().Be("Shop.Data.SqlCommand.new");
    }

    [Fact]
    public void LayersWithMembers_ThrowsAtCompile()
    {
        var pack = PackWith(Forbid(new TargetSelector { Layers = ["Infrastructure"], Members = ["A.B.C"] }));
        var act = () => ForbidRules.Compile(new LayerMap(pack), pack);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TypeRuleAndMembersRule_EachReportOnce()
    {
        var typeRule = new RuleDefinition
        {
            Id = "TEST-TYPES",
            Because = "No SQL types.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Namespaces = ["**.Domain.**"] },
            To = new TargetSelector { Types = ["Shop.Data.SqlCommand"] },
        };
        var pack = new RulePack { Name = "test", Rules = [typeRule, Forbid(Members("Shop.Data.SqlCommand.Timeout"))] };

        var violations = Check("""
            namespace Shop.Domain { public class Order { public int Run(Data.SqlCommand c) => c.Timeout; } }
            """, pack);

        violations.Should().HaveCount(2);
        violations.Single(v => string.Equals(v.RuleId, "TEST-TYPES", StringComparison.Ordinal)).Target.Should().Be("Shop.Data.SqlCommand");
        violations.Single(v => string.Equals(v.RuleId, "TEST-MEMBERS", StringComparison.Ordinal)).Target.Should().Be("Shop.Data.SqlCommand.Timeout");
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source, RulePack pack, ViolationKind? kind = null)
    {
        var compilation = TestCompilation.Create(("Data.cs", DataTypes), ("Test.cs", source));
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), MemberNameFilter.From(pack), CancellationToken.None);
        var layers = new LayerMap(pack);
        var violations = kind == ViolationKind.DependencyNotPermitted
            ? AllowOnlyRules.Check(graph, layers, pack)
            : ForbidRules.Check(graph, layers, pack);
        return violations.Where(v => v.SourceType.StartsWith("Shop.Domain", StringComparison.Ordinal) || v.SourceType.StartsWith("Shop.Infrastructure", StringComparison.Ordinal)).ToList();
    }
}
