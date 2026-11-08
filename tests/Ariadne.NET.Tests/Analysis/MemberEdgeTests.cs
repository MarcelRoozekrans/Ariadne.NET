using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class MemberEdgeTests
{
    private const string Order = "Shop.Domain.Order";

    private const string DataTypes = """
        namespace Shop.Data
        {
            public interface IDbCommand { int ExecuteReader(); string? Text { get; set; } }
            public abstract class DbCommand : IDbCommand
            {
                public abstract int ExecuteReader();
                public virtual string? Text { get; set; }
                public virtual event System.EventHandler? Completed;
                protected void Raise() => Completed?.Invoke(this, System.EventArgs.Empty);
            }
            public class SqlCommand : DbCommand
            {
                public SqlCommand() { }
                public SqlCommand(string text) => Text = text;
                public override int ExecuteReader() => 0;
                public override string? Text { get; set; }
                public override event System.EventHandler? Completed;
                public int Timeout;
                public static int Count;
            }
            public sealed class Runner : IDbCommand
            {
                public int ExecuteReader() => 1;
                public string? Text { get; set; }
            }
            public sealed class Explicit : IDbCommand
            {
                int IDbCommand.ExecuteReader() => 2;
                string? IDbCommand.Text { get; set; }
            }
        }
        """;

    [Fact]
    public void Call_RecordsEdgeWithLocation()
    {
        var graph = Build(["Shop.Data.SqlCommand.ExecuteReader"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public int Place(Shop.Data.SqlCommand command) => command.ExecuteReader();
                }
            }
            """);

        graph.MemberEdges.Should().ContainSingle();
        var edge = graph.MemberEdges[0];
        edge.Source.ToDisplayString().Should().Be(Order);
        MemberNames.Chain(edge.Member)[0].Should().Be("Shop.Data.SqlCommand.ExecuteReader");
        edge.Location.Should().Be(new SourceLocation("Order.cs", 5, 67));
    }

    [Fact]
    public void ConstructorCall_RecordsCtor()
    {
        var graph = Build(["Shop.Data.SqlCommand.new"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public void Place()
                    {
                        var a = new Shop.Data.SqlCommand("x");
                        Shop.Data.SqlCommand b = new();
                    }
                }
            }
            """);

        Uses(graph).Should().Equal(
            ("Shop.Data.SqlCommand.new", 7, 21),
            ("Shop.Data.SqlCommand.new", 8, 38));
    }

    [Fact]
    public void PropertyAndFieldAccess_Recorded()
    {
        var graph = Build(["**.SqlCommand.Text", "**.SqlCommand.Timeout", "**.SqlCommand.Count"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public void Place(Shop.Data.SqlCommand command)
                    {
                        command.Text = "select 1";
                        var text = command.Text;
                        command.Timeout = Shop.Data.SqlCommand.Count;
                        var other = new Shop.Data.SqlCommand { Timeout = 3 };
                    }
                }
            }
            """);

        Uses(graph).Should().Equal(
            ("Shop.Data.SqlCommand.Text", 7, 21),
            ("Shop.Data.SqlCommand.Text", 8, 32),
            ("Shop.Data.SqlCommand.Timeout", 9, 21),
            ("Shop.Data.SqlCommand.Count", 9, 52),
            ("Shop.Data.SqlCommand.Timeout", 10, 52));
    }

    [Fact]
    public void EventSubscription_Recorded()
    {
        var graph = Build(["**.SqlCommand.Completed"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public void Place(Shop.Data.SqlCommand command) => command.Completed += OnCompleted;
                    private void OnCompleted(object? sender, System.EventArgs e) { }
                }
            }
            """);

        Uses(graph).Should().Equal(("Shop.Data.SqlCommand.Completed", 5, 68));
    }

    [Fact]
    public void MethodGroup_Recorded()
    {
        var graph = Build(["**.ExecuteReader"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public System.Func<int> Place(Shop.Data.SqlCommand command) => command.ExecuteReader;
                }
            }
            """);

        Uses(graph).Should().Equal(("Shop.Data.SqlCommand.ExecuteReader", 5, 80));
    }

    [Fact]
    public void ExtensionMethodCall_Recorded()
    {
        var graph = Build(["**.Run*"], """
            namespace Shop.Infra
            {
                public static class CommandExtensions
                {
                    public static int RunTwice(this Shop.Data.SqlCommand command) => 2;
                    public static T RunAs<T>(this T value) => value;
                }
            }
            namespace Shop.Domain
            {
                using Shop.Infra;
                public class Order
                {
                    public int Place(Shop.Data.SqlCommand command) => command.RunTwice() + command.RunAs().Timeout;
                }
            }
            """);

        Uses(graph).Should().Equal(
            ("Shop.Infra.CommandExtensions.RunTwice", 14, 67),
            ("Shop.Infra.CommandExtensions.RunAs", 14, 88));
        graph.MemberEdges.Should().OnlyContain(e => e.Member.Equals(e.Member.OriginalDefinition, SymbolEqualityComparer.Default));
    }

    [Fact]
    public void ExtensionBlockMemberCall_NamedAfterTheStaticClass()
    {
        var graph = Build(["**.Persist"], """
            namespace Shop.Infra
            {
                public static class Store
                {
                    extension(Shop.Data.SqlCommand command)
                    {
                        public void Persist() { }
                    }
                }
            }
            namespace Shop.Domain
            {
                using Shop.Infra;
                public class Order { public void Place(Shop.Data.SqlCommand command) => command.Persist(); }
            }
            """);

        Uses(graph).Should().Equal(("Shop.Infra.Store.Persist", 14, 85));
    }

    [Fact]
    public void NameOf_NotRecorded()
    {
        var graph = Build(["**"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public string Place() => nameof(Shop.Data.SqlCommand.ExecuteReader) + nameof(Shop.Data.SqlCommand.Count);
                }
            }
            """);

        Uses(graph).Should().BeEmpty();
    }

    [Fact]
    public void Declaration_NotRecorded()
    {
        var graph = Build(["**"], """
            namespace Shop.Domain
            {
                public class Order : Shop.Data.DbCommand
                {
                    private int _count;
                    public override int ExecuteReader() => 0;
                    public System.Type Kind() => typeof(Shop.Data.SqlCommand);
                }
            }
            """);

        graph.MemberEdges.Where(e => string.Equals(e.Source.ToDisplayString(), Order, StringComparison.Ordinal)).Should().BeEmpty();
        graph.Edges.Should().Contain(e => string.Equals(e.Target.ToDisplayString(), "Shop.Data.SqlCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void Override_ChainIncludesOverriddenMember()
    {
        var graph = Build(["**.ExecuteReader", "**.Text", "**.Completed"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public int Place(Shop.Data.SqlCommand command)
                    {
                        command.Completed += (_, _) => { };
                        command.Text = "x";
                        return command.ExecuteReader();
                    }
                }
            }
            """);

        Chains(graph).Should().Equal(
            "Shop.Data.SqlCommand.Completed > Shop.Data.DbCommand.Completed",
            "Shop.Data.SqlCommand.Text > Shop.Data.DbCommand.Text > Shop.Data.IDbCommand.Text",
            "Shop.Data.SqlCommand.ExecuteReader > Shop.Data.DbCommand.ExecuteReader > Shop.Data.IDbCommand.ExecuteReader");
    }

    [Fact]
    public void InterfaceImplementation_ChainIncludesInterfaceMember()
    {
        var graph = Build(["**.ExecuteReader"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public int Place(Shop.Data.Runner runner) => runner.ExecuteReader();
                }
            }
            """);

        Chains(graph).Should().Equal("Shop.Data.Runner.ExecuteReader > Shop.Data.IDbCommand.ExecuteReader");
    }

    [Fact]
    public void ExplicitInterfaceImplementation_ChainIncludesInterfaceMember()
    {
        var compilation = TestCompilation.Create(("Data.cs", DataTypes));
        var explicitImpl = compilation.GetTypeByMetadataName("Shop.Data.Explicit")!
            .GetMembers().OfType<IMethodSymbol>().First(m => m.MethodKind == MethodKind.ExplicitInterfaceImplementation);

        // The implementation's own name is never used, because code reaches it through the interface member.
        MemberNames.Chain(explicitImpl).Should().HaveCount(2).And.EndWith("Shop.Data.IDbCommand.ExecuteReader");
    }

    [Fact]
    public void CallThroughInterface_ChainStaysOnInterface()
    {
        var graph = Build(["**.ExecuteReader"], """
            namespace Shop.Domain
            {
                public class Order
                {
                    public int Place(Shop.Data.IDbCommand command) => command.ExecuteReader();
                }
            }
            """);

        Chains(graph).Should().Equal("Shop.Data.IDbCommand.ExecuteReader");
    }

    [Fact]
    public void GenericMember_UsesOriginalDefinition()
    {
        var graph = Build(["**.Repo.Save", "**.Repo.Name", "**.Factory.Make", "**.Repo.Line.Close"], """
            namespace Shop.Infra
            {
                public class Repo<T>
                {
                    public string? Name { get; set; }
                    public void Save(T entity) { }
                    public class Line { public void Close() { } }
                }
                public static class Factory { public static T Make<T>() where T : new() => new T(); }
            }
            namespace Shop.Domain
            {
                public class Order
                {
                    public void Place()
                    {
                        var repo = Shop.Infra.Factory.Make<Shop.Infra.Repo<Order>>();
                        repo.Save(this);
                        repo.Name = "orders";
                        new Shop.Infra.Repo<int>.Line().Close();
                    }
                }
            }
            """);

        Uses(graph).Should().Equal(
            ("Shop.Infra.Factory.Make", 17, 43),
            ("Shop.Infra.Repo.Save", 18, 18),
            ("Shop.Infra.Repo.Name", 19, 18),
            ("Shop.Infra.Repo.Line.Close", 20, 45));
        graph.MemberEdges.Should().OnlyContain(e => e.Member.Equals(e.Member.OriginalDefinition, SymbolEqualityComparer.Default));
    }

    [Fact]
    public void EmptyFilter_RecordsNoMemberEdges()
    {
        var graph = TypeGraphBuilder.Build(
            TestCompilation.Create(("Data.cs", DataTypes), ("Order.cs", UsesEverything)),
            new HashSet<SyntaxTree>(),
            MemberNameFilter.From(PackWith()),
            CancellationToken.None);

        graph.MemberEdges.Should().BeEmpty();
        graph.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public void GeneratedCode_RecordsNoMemberEdges()
    {
        var compilation = TestCompilation.Create(
            ("Data.cs", DataTypes),
            ("Order.g.cs", UsesEverything),
            ("Tool.cs", """
                namespace Shop.Domain
                {
                    [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                    public class Tool { public int Run(Shop.Data.SqlCommand command) => command.ExecuteReader(); }
                }
                """));

        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), MemberNameFilter.From(PackWith("**")), CancellationToken.None);

        graph.MemberEdges.Should().OnlyContain(e => string.Equals(e.Location.FilePath, "Data.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void FilterWithoutMatchingName_RecordsNothing()
    {
        var graph = Build(["Shop.Data.SqlCommand.Dispose*"], UsesEverything);

        graph.MemberEdges.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Shop.Data.DbCommand.Execute*", "ExecuteReader", true)]
    [InlineData("Shop.Data.DbCommand.Execute*", "Text", false)]
    [InlineData("System.Diagnostics.ProcessStartInfo.new", "new", true)]
    [InlineData("Shop.Data.SqlCommand.*", "Anything", true)]
    [InlineData("Shop.Data.**", "Anything", true)]
    public void Filter_ComparesTheMemberSegment(string pattern, string memberName, bool expected) =>
        MemberNameFilter.From(PackWith(pattern)).CouldMatch(memberName).Should().Be(expected);

    // Interim: measures with GC.GetAllocatedBytesForCurrentThread, see ZeroAlloc-Net/ZeroAlloc.TestHelpers#106;
    // AllocationGate is not referenced in this repo yet.
    [Fact]
    public void Filter_CouldMatch_AllocatesNothing()
    {
        var empty = MemberNameFilter.From(PackWith());
        var filter = MemberNameFilter.From(PackWith("**.DbCommand.Execute*", "**.Process.Start"));
        filter.CouldMatch("ExecuteReader").Should().BeTrue();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            empty.CouldMatch("ExecuteReader");
            filter.CouldMatch("ExecuteReader");
            filter.CouldMatch("Text");
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    private const string UsesEverything = """
        namespace Shop.Domain
        {
            public class Order
            {
                public int Place(Shop.Data.SqlCommand command)
                {
                    var created = new Shop.Data.SqlCommand("x");
                    command.Text = created.Text;
                    command.Completed += (_, _) => { };
                    System.Func<int> run = command.ExecuteReader;
                    return command.ExecuteReader() + command.Timeout;
                }
            }
        }
        """;

    internal static RulePack PackWith(params string[] members) => new()
    {
        Name = "p",
        Rules =
        [
            new RuleDefinition
            {
                Id = "R-1",
                Because = "Test.",
                Kind = RuleKind.Forbid,
                From = new TargetSelector { Namespaces = ["**"] },
                To = new TargetSelector { Members = members },
            },
        ],
    };

    private static TypeGraph Build(string[] members, string orderSource) =>
        TypeGraphBuilder.Build(
            TestCompilation.Create(("Data.cs", DataTypes), ("Order.cs", orderSource)),
            new HashSet<SyntaxTree>(),
            MemberNameFilter.From(PackWith(members)),
            CancellationToken.None);

    private static List<(string Member, int Line, int Column)> Uses(TypeGraph graph) =>
        graph.MemberEdges
            .Where(e => string.Equals(e.Source.ToDisplayString(), Order, StringComparison.Ordinal))
            .Select(e => (MemberNames.Chain(e.Member)[0], e.Location.Line, e.Location.Column))
            .ToList();

    // Each chain joined with ' > ', so the chains compare by value.
    private static List<string> Chains(TypeGraph graph) =>
        graph.MemberEdges
            .Where(e => string.Equals(e.Source.ToDisplayString(), Order, StringComparison.Ordinal))
            .Select(e => string.Join(" > ", MemberNames.Chain(e.Member)))
            .ToList();
}
