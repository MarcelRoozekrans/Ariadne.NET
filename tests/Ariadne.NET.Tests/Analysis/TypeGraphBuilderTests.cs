using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class TypeGraphBuilderTests
{
    private const string Order = "Shop.Domain.Order";
    private const string Store = "Shop.Infra.Store";

    private const string StoreClass = """
        namespace Shop.Infra
        {
            public class Store
            {
                public static int Count;
                public static Store Make() => new();
                public void Save() { }
            }
        }
        """;

    private static readonly HashSet<SyntaxTree> NoGeneratedTrees = [];

    // Declaration-level references

    [Fact]
    public void BaseType() => AssertOrderToStore("""
        namespace Shop.Infra { public class Store { } }
        namespace Shop.Domain { public class Order : Shop.Infra.Store { } }
        """);

    [Fact]
    public void Interface() => AssertOrderToStore("""
        namespace Shop.Infra { public interface Store { } }
        namespace Shop.Domain { public class Order : Shop.Infra.Store { } }
        """);

    [Fact]
    public void Attribute_OnType() => AssertOrderToStore("""
        namespace Shop.Infra { public sealed class Store : System.Attribute { } }
        namespace Shop.Domain { [Shop.Infra.Store] public class Order { } }
        """);

    [Fact]
    public void Attribute_OnMember() => AssertOrderToStore("""
        namespace Shop.Infra { public sealed class Store : System.Attribute { } }
        namespace Shop.Domain { public class Order { [Shop.Infra.Store] public void Place() { } } }
        """);

    [Fact]
    public void FieldType() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { private Shop.Infra.Store? _store; } }
        """);

    [Fact]
    public void PropertyType() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public Shop.Infra.Store? Store { get; set; } } }
        """);

    [Fact]
    public void ParameterType() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public void Place(Shop.Infra.Store store) { } } }
        """);

    [Fact]
    public void ReturnType() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public Shop.Infra.Store? Find() => null; } }
        """);

    [Fact]
    public void GenericArgument() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { private System.Collections.Generic.List<Shop.Infra.Store>? _stores; } }
        """);

    [Fact]
    public void GenericConstraint() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public void Place<T>() where T : Shop.Infra.Store { } } }
        """);

    [Fact]
    public void ArrayElementType() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { private Shop.Infra.Store[]? _stores; } }
        """);

    // Body references

    [Fact]
    public void ObjectCreation() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public object Place() => new Shop.Infra.Store(); } }
        """);

    [Fact]
    public void ImplicitObjectCreation()
    {
        var edges = Edges(StoreClass + """
            namespace Shop.Domain
            {
                using Shop.Infra;
                public class Order { public void Place(Store store) { } public void Run() => Place(new()); }
            }
            """);

        // One edge from the parameter type, one from the target-typed new.
        edges.Should().Equal((Order, Store), (Order, Store));
    }

    [Fact]
    public void StaticMemberAccess() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public int Place() => Shop.Infra.Store.Count; } }
        """);

    [Fact]
    public void InstanceMemberAccess() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain
        {
            public class Order
            {
                public void Save(Holder holder) => holder.Store.Save();
            }
            public class Holder { public Shop.Infra.Store Store { get; } = new(); }
        }
        """);

    [Fact]
    public void ExtensionMethodCall()
    {
        var edges = Edges("""
            namespace Shop.Infra { public static class Store { public static void Persist(this Shop.Domain.Order order) { } } }
            namespace Shop.Domain
            {
                using Shop.Infra;
                public class Order { public void Place() => this.Persist(); }
            }
            """);

        edges.Should().Contain((Order, Store));
    }

    [Fact]
    public void ExtensionBlockMemberCall()
    {
        var edges = Edges("""
            namespace Shop.Infra
            {
                public static class Store
                {
                    extension(Shop.Domain.Order order)
                    {
                        public void Persist() { }
                    }
                }
            }
            namespace Shop.Domain
            {
                using Shop.Infra;
                public class Order { public void Place() => this.Persist(); }
            }
            """);

        edges.Should().Contain((Order, Store));
        edges.Should().OnlyContain(e => string.Equals(e.Source, Order, StringComparison.Ordinal) || string.Equals(e.Source, Store, StringComparison.Ordinal));
    }

    [Fact]
    public void TypeOf() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public System.Type Place() => typeof(Shop.Infra.Store); } }
        """);

    [Fact]
    public void Cast() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public object Place(object o) => (Shop.Infra.Store)o; } }
        """);

    [Fact]
    public void IsPattern() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { public bool Place(object o) => o is Shop.Infra.Store; } }
        """);

    [Fact]
    public void CatchClause() => AssertOrderToStore("""
        namespace Shop.Infra { public class Store : System.Exception { } }
        namespace Shop.Domain
        {
            public class Order
            {
                public void Place()
                {
                    try { } catch (Shop.Infra.Store) { }
                }
            }
        }
        """);

    // Naming forms

    [Fact]
    public void NestedTypeReference()
    {
        var edges = Edges("""
            namespace Shop.Infra { public class Outer { public class Inner { } } }
            namespace Shop.Domain { public class Order { private Shop.Infra.Outer.Inner? _inner; } }
            """);

        edges.Should().Equal((Order, "Shop.Infra.Outer"), (Order, "Shop.Infra.Outer.Inner"));
    }

    [Fact]
    public void FullyQualifiedName_NoUsing() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain { public class Order { private global::Shop.Infra.Store? _store; } }
        """);

    [Fact]
    public void AliasUse() => AssertOrderToStore(StoreClass + """
        namespace Shop.Domain
        {
            using S = Shop.Infra.Store;
            public class Order { private S? _store; }
        }
        """);

    [Fact]
    public void GlobalUsingUse()
    {
        var edges = Edges(
            ("GlobalUsings.cs", "global using Shop.Infra;"),
            ("Store.cs", StoreClass),
            ("Order.cs", "namespace Shop.Domain { public class Order { private Store? _store; } }"));

        edges.Should().Contain((Order, Store));
    }

    [Fact]
    public void UsingInsideFileScopedNamespace_WithUse()
    {
        var graph = Build(
            ("Store.cs", "namespace Shop.Infra; public class Store { }"),
            ("Order.cs", """
                namespace Shop.Domain;

                using Shop.Infra;

                public class Order
                {
                    private Store? _store;
                }
                """));

        graph.Edges.Should().ContainSingle();
        var edge = graph.Edges[0];
        Pair(edge).Should().Be((Order, Store));
        edge.Location.Should().Be(new SourceLocation("Order.cs", 7, 13));
    }

    [Fact]
    public void AliasToConstructedGeneric_GivesEdgeToTypeArgument() => AssertSingleOrderToStoreAt(
        """
        namespace Shop.Domain
        {
            using Stores = System.Collections.Generic.List<Shop.Infra.Store>;
            public class Order
            {
                private Stores? _stores;
            }
        }
        """,
        line: 6,
        column: 17);

    [Fact]
    public void TupleAlias_GivesEdgeToElementType() => AssertSingleOrderToStoreAt(
        """
        namespace Shop.Domain
        {
            using Pair = (Shop.Infra.Store Store, int Count);
            public class Order
            {
                private Pair _pair;
            }
        }
        """,
        line: 6,
        column: 17);

    [Fact]
    public void DelegateAlias_GivesEdgeToTypeArgument() => AssertSingleOrderToStoreAt(
        """
        namespace Shop.Domain
        {
            using Find = System.Func<Shop.Infra.Store>;
            public class Order
            {
                private Find? _find;
            }
        }
        """,
        line: 6,
        column: 17);

    [Fact]
    public void UsingStaticOfConstructedGeneric_GivesEdgeToTypeArgument()
    {
        var graph = Build(
            ("Store.cs", StoreClass + "\nnamespace Shop.Util { public static class Helper<T> { public static void Foo() { } } }"),
            ("Order.cs", """
                using static Shop.Util.Helper<Shop.Infra.Store>;

                namespace Shop.Domain
                {
                    public class Order
                    {
                        public void Place() => Foo();
                    }
                }
                """));

        AssertSingleOrderToStoreAt(graph, "Order.cs", line: 7, column: 32);
        graph.Edges.Select(Pair).Should().Contain((Order, "Shop.Util.Helper<T>"));
    }

    [Fact]
    public void AliasToNestedType_GivesEdgesToOuterAndInner()
    {
        var graph = Build(
            ("Store.cs", "namespace Shop.Infra { public class Outer { public class Inner { } } }"),
            ("Order.cs", """
                namespace Shop.Domain
                {
                    using Nested = Shop.Infra.Outer.Inner;
                    public class Order
                    {
                        private Nested? _nested;
                    }
                }
                """));

        graph.Edges.Select(Pair).Should().Equal((Order, "Shop.Infra.Outer"), (Order, "Shop.Infra.Outer.Inner"));
        graph.Edges.Should().OnlyContain(e => e.Location == new SourceLocation("Order.cs", 6, 17));
    }

    [Fact]
    public void OverloadedMethodGroup_BindsThroughCandidateSymbol()
    {
        var graph = Build(
            ("Store.cs", """
                namespace Shop.Infra
                {
                    public class Store { public void Overloaded() { } public void Overloaded(int count) { } }
                    public class Holder { public Store Value { get; } = new(); }
                }
                """),
            ("Order.cs", """
                namespace Shop.Domain
                {
                    public class Order
                    {
                        public string Name(Shop.Infra.Holder holder) => nameof(holder.Value.Overloaded);
                    }
                }
                """));

        AssertSingleOrderToStoreAt(graph, "Order.cs", line: 5, column: 77);
    }

    [Fact]
    public void PointerElementType()
    {
        var graph = BuildUnsafe("""
            namespace Shop.Domain { public unsafe class Order { private Shop.Infra.Store* _store; } }
            """);

        AssertSingleOrderToStoreAt(graph, "Order.cs", line: 1, column: 72);
    }

    [Fact]
    public void PointerAlias_GivesEdgeToElementType()
    {
        var graph = BuildUnsafe("""
            namespace Shop.Domain
            {
                using unsafe StorePointer = Shop.Infra.Store*;
                public unsafe class Order
                {
                    private StorePointer _store;
                }
            }
            """);

        AssertSingleOrderToStoreAt(graph, "Order.cs", line: 6, column: 17);
    }

    // No edge

    [Fact]
    public void UnusedUsing_NoEdge()
    {
        var edges = Edges(StoreClass + """
            namespace Shop.Domain
            {
                using Shop.Infra;
                using S = Shop.Infra.Store;
                public class Order { }
            }
            """);

        edges.Should().BeEmpty();
    }

    [Fact]
    public void UnusedUsingInFileScopedNamespace_NoEdge()
    {
        var edges = Edges(
            ("Store.cs", StoreClass),
            ("Order.cs", "namespace Shop.Domain;\nusing Shop.Infra;\npublic class Order { }"));

        edges.Should().BeEmpty();
    }

    [Fact]
    public void Var_NoEdge()
    {
        var edges = Edges(StoreClass + """
            namespace Shop.Domain
            {
                public static class Factory { public static Shop.Infra.Store Make() => new(); }
                public class Order { public void Place() { var x = Factory.Make(); _ = x; } }
            }
            """);

        edges.Where(e => string.Equals(e.Source, Order, StringComparison.Ordinal))
            .Should().OnlyContain(e => string.Equals(e.Target, "Shop.Domain.Factory", StringComparison.Ordinal))
            .And.NotBeEmpty();
    }

    [Fact]
    public void SelfReference_NoEdge()
    {
        var edges = Edges("""
            namespace Shop.Domain
            {
                public class Order
                {
                    private Order? _parent;
                    public static Order Create() => new Order();
                    public Order Copy() => Order.Create();
                }
            }
            """);

        edges.Should().BeEmpty();
    }

    [Fact]
    public void TypeParameter_NoEdge()
    {
        var edges = Edges("""
            namespace Shop.Domain
            {
                public class Order<T> { private T? _item; public TOther? Map<TOther>(T item) => default; }
            }
            """);

        edges.Should().BeEmpty();
    }

    [Fact]
    public void AssemblyAttribute_NoEdge()
    {
        var edges = Edges("""
            [assembly: Shop.Infra.Store]
            namespace Shop.Infra { public sealed class Store : System.Attribute { } }
            namespace Shop.Domain { public class Order { } }
            """);

        edges.Should().Equal((Store, "System.Attribute"));
    }

    [Fact]
    public void Dynamic_NoEdge()
    {
        var edges = Edges("""
            namespace Shop.Domain { public class Order { private dynamic? _value; public void Place() => _value!.Go(); } }
            """);

        edges.Should().BeEmpty();
    }

    // Generated code

    [Fact]
    public void GeneratedTree_NoEdges_ButTypeIsTarget()
    {
        var graph = Build(
            ("Store.g.cs", "namespace Shop.Infra { public class Store { private Shop.Domain.Order? _order; } }"),
            ("Order.cs", "namespace Shop.Domain { public class Order { private Shop.Infra.Store? _store; } }"));

        graph.Edges.Select(Pair).Should().Equal((Order, Store));
        graph.SourceTypes.Select(t => t.ToDisplayString()).Should().Equal(Order);
    }

    [Fact]
    public void GeneratorOutputTree_NoEdges()
    {
        var compilation = TestCompilation.Create(
            ("Store.cs", "namespace Shop.Infra { public class Store { private Shop.Domain.Order? _order; } }"),
            ("Order.cs", "namespace Shop.Domain { public class Order { } }"));

        var graph = TypeGraphBuilder.Build(
            compilation, new HashSet<SyntaxTree> { compilation.SyntaxTrees[0] }, CancellationToken.None);

        graph.Edges.Should().BeEmpty();
    }

    [Fact]
    public void PartialTypeSplitOverGeneratedAndHandWritten_EdgesFromHandWrittenOnly()
    {
        var graph = Build(
            ("Store.cs", StoreClass + "\nnamespace Shop.Other { public class Gen { } }"),
            ("Order.g.cs", "namespace Shop.Domain { public partial class Order { private Shop.Other.Gen? _gen; } }"),
            ("Order.cs", "namespace Shop.Domain { public partial class Order { private Shop.Infra.Store? _store; } }"));

        graph.Edges.Select(Pair).Should().Equal((Order, Store));
        graph.SourceTypes.Select(t => t.ToDisplayString()).Should().Contain(Order);
    }

    [Fact]
    public void GeneratedCodeAttributeType_NoEdges_ButTypeIsTarget()
    {
        var edges = Edges("""
            namespace Shop.Infra
            {
                [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                public class Store { private Shop.Domain.Order? _order; public class Nested { private Shop.Domain.Order? _order; } }
            }
            namespace Shop.Domain { public class Order { private Shop.Infra.Store? _store; } }
            """);

        edges.Should().Equal((Order, Store));
    }

    // Bookkeeping

    [Fact]
    public void NestedType_IsSourceOfItsOwnEdges()
    {
        var edges = Edges(StoreClass + """
            namespace Shop.Domain
            {
                public class Order
                {
                    public class Line { private Shop.Infra.Store? _store; }
                }
            }
            """);

        edges.Should().Equal(("Shop.Domain.Order.Line", Store));
    }

    [Fact]
    public void NestedType_ReferencingOuter_GivesEdge()
    {
        var edges = Edges("""
            namespace Shop.Domain
            {
                public class Order
                {
                    public class Line { private Order? _order; }
                }
            }
            """);

        edges.Should().Equal(("Shop.Domain.Order.Line", Order));
    }

    [Fact]
    public void Location_IsLineAndColumnOfTheName()
    {
        var graph = Build(
            ("Store.cs", "namespace Shop.Infra { public class Store { } public class Box<T> { } }"),
            ("Order.cs", """
                namespace Shop.Domain
                {
                    public class Order
                    {
                        private Shop.Infra.Box<Shop.Infra.Store>? _box;
                    }
                }
                """));

        graph.Edges.Should().Equal(
            new TypeEdge(Type(graph, Order), Type(graph, "Shop.Infra.Box<T>"), new SourceLocation("Order.cs", 5, 28)),
            new TypeEdge(Type(graph, Order), Type(graph, Store), new SourceLocation("Order.cs", 5, 43)));
    }

    [Fact]
    public void DuplicateReferences_OneEdgePerLocation()
    {
        var graph = Build(
            ("Store.cs", StoreClass),
            ("Order.cs", """
                namespace Shop.Domain
                {
                    using Shop.Infra;
                    public class Order
                    {
                        private Store? _a;
                        private Store? _b;
                        public void Place() { Store.Make().Save(); }
                    }
                }
                """));

        graph.Edges.Select(e => (e.Location.Line, e.Location.Column)).Should().Equal(
            (6, 17), (7, 17), (8, 31), (8, 37), (8, 44));
        graph.Edges.Select(Pair).Should().AllBeEquivalentTo((Order, Store));
    }

    [Fact]
    public void SourceTypes_ExcludesGeneratedTypes()
    {
        var graph = Build(
            ("Order.cs", """
                namespace Shop.Domain
                {
                    public class Order { public class Line { } }
                    [System.CodeDom.Compiler.GeneratedCode("tool", "1.0")]
                    public class Tooling { public class Nested { } }
                    public delegate void Placed();
                    public enum Status { Open }
                }
                """),
            ("Store.g.cs", "namespace Shop.Infra { public class Store { } }"));

        graph.SourceTypes.Select(t => t.ToDisplayString()).Should().Equal(
            Order, "Shop.Domain.Order.Line", "Shop.Domain.Placed", "Shop.Domain.Status");
    }

    [Fact]
    public void SourceTypes_OrderedByFirstDeclaration()
    {
        var graph = Build(
            ("B.cs", "namespace Shop { public class Beta { } public class Alpha { } }"),
            ("A.cs", "namespace Shop { public class Gamma { } }"));

        graph.SourceTypes.Select(t => t.ToDisplayString()).Should().Equal("Shop.Gamma", "Shop.Beta", "Shop.Alpha");
    }

    [Fact]
    public void TopLevelStatements_SourceIsProgram()
    {
        var library = TestCompilation.Create(("Store.cs", StoreClass));
        var compilation = library
            .WithOptions(library.Options.WithOutputKind(OutputKind.ConsoleApplication))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("var count = Shop.Infra.Store.Count;\n_ = count;", path: "Program.cs"));
        compilation.GetDiagnostics().Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var graph = TypeGraphBuilder.Build(compilation, NoGeneratedTrees, CancellationToken.None);

        graph.Edges.Select(Pair).Should().Equal(("Program", Store), ("Program", Store));
        graph.SourceTypes.Select(t => t.ToDisplayString()).Should().Contain("Program");
    }

    private static void AssertSingleOrderToStoreAt(string orderSource, int line, int column) =>
        AssertSingleOrderToStoreAt(Build(("Store.cs", StoreClass), ("Order.cs", orderSource)), "Order.cs", line, column);

    private static void AssertSingleOrderToStoreAt(TypeGraph graph, string path, int line, int column)
    {
        var toStore = graph.Edges.Where(e => Pair(e) == (Order, Store)).ToList();
        toStore.Should().ContainSingle();
        toStore[0].Location.Should().Be(new SourceLocation(path, line, column));
    }

    private static TypeGraph BuildUnsafe(string orderSource)
    {
        var library = TestCompilation.Create(("Store.cs", "namespace Shop.Infra { public struct Store { } }"));
        var compilation = library
            .WithOptions(library.Options.WithAllowUnsafe(true))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(orderSource, path: "Order.cs"));
        compilation.GetDiagnostics().Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        return TypeGraphBuilder.Build(compilation, NoGeneratedTrees, CancellationToken.None);
    }

    private static void AssertOrderToStore(string source) => Edges(source).Should().Contain((Order, Store));

    private static List<(string Source, string Target)> Edges(string source) =>
        Build(("Test.cs", source)).Edges.Select(Pair).ToList();

    private static List<(string Source, string Target)> Edges(params (string Path, string Source)[] files) =>
        Build(files).Edges.Select(Pair).ToList();

    private static TypeGraph Build(params (string Path, string Source)[] files) =>
        TypeGraphBuilder.Build(TestCompilation.Create(files), NoGeneratedTrees, CancellationToken.None);

    private static (string Source, string Target) Pair(TypeEdge edge) =>
        (edge.Source.ToDisplayString(), edge.Target.ToDisplayString());

    private static INamedTypeSymbol Type(TypeGraph graph, string displayName)
    {
        foreach (var edge in graph.Edges)
        {
            if (string.Equals(edge.Source.ToDisplayString(), displayName, StringComparison.Ordinal))
            {
                return edge.Source;
            }

            if (string.Equals(edge.Target.ToDisplayString(), displayName, StringComparison.Ordinal))
            {
                return edge.Target;
            }
        }

        throw new InvalidOperationException("No type " + displayName + " in the graph.");
    }
}
