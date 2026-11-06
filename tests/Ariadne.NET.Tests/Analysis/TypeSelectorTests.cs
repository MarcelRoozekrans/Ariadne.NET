using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public class TypeSelectorTests
{
    private static INamedTypeSymbol Find(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) ?? throw new InvalidOperationException($"{metadataName} not found.");

    [Fact]
    public void Namespace_Matches()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain.Orders { public class Order { } }");
        var order = Find(compilation, "Shop.Domain.Orders.Order");

        TypeSelector.From(["**.Domain.**"], [], []).Matches(order, new TypeNames()).Should().BeTrue();
        TypeSelector.From(["**.Infrastructure.**"], [], []).Matches(order, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void Assembly_Matches()
    {
        var compilation = TestCompilation.Create("namespace N { public class A { } }");
        var a = Find(compilation, "N.A");

        TypeSelector.From([], ["TestAssembly"], []).Matches(a, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], ["Other*"], []).Matches(a, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void Type_MatchesFullNameWithoutArity()
    {
        var compilation = TestCompilation.Create("namespace N { public class Foo<T> { } }");
        var foo = Find(compilation, "N.Foo`1");

        TypeSelector.From([], [], ["N.Foo"]).Matches(foo, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], ["N.Foo`1"]).Matches(foo, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void NestedType_MatchesOuterDotInner()
    {
        var compilation = TestCompilation.Create("public class Outer { public class Inner { } }");
        var inner = Find(compilation, "Outer+Inner");

        TypeSelector.From([], [], ["Outer.Inner"]).Matches(inner, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], ["**.Inner"]).Matches(inner, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], ["Inner"]).Matches(inner, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void NestedTypeInNamespace_MatchesNamespaceOuterInner()
    {
        var compilation = TestCompilation.Create("namespace N { public class Outer { public class Inner { } } }");
        var inner = Find(compilation, "N.Outer+Inner");

        TypeSelector.From([], [], ["N.Outer.Inner"]).Matches(inner, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], ["N.Inner"]).Matches(inner, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void GlobalNamespaceType_MatchesDoubleStar()
    {
        var compilation = TestCompilation.Create("public class Loose { }");
        var loose = Find(compilation, "Loose");

        TypeSelector.From(["**"], [], []).Matches(loose, new TypeNames()).Should().BeTrue();
        TypeSelector.From(["*"], [], []).Matches(loose, new TypeNames()).Should().BeFalse();
        TypeSelector.From(["Shop"], [], []).Matches(loose, new TypeNames()).Should().BeFalse();
        TypeSelector.From([], [], ["Loose"]).Matches(loose, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void AssemblyAndNamespace_AreAnded()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class A { } }");
        var a = Find(compilation, "Shop.Domain.A");

        TypeSelector.From(["Shop.**"], ["TestAssembly"], []).Matches(a, new TypeNames()).Should().BeTrue();
        TypeSelector.From(["Shop.**"], ["Other"], []).Matches(a, new TypeNames()).Should().BeFalse();
        TypeSelector.From(["Other.**"], ["TestAssembly"], []).Matches(a, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void TwoNamespaces_AreOred()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class A { } }");
        var a = Find(compilation, "Shop.Domain.A");

        TypeSelector.From(["Other", "Shop.Domain"], [], []).Matches(a, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void EmptySelector_MatchesEverything()
    {
        var compilation = TestCompilation.Create("namespace N { public class A { } }");

        TypeSelector.From([], [], []).Matches(Find(compilation, "N.A"), new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void InvalidPattern_Throws()
    {
        var act = () => TypeSelector.From(["A..B"], [], []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NullArguments_Throw()
    {
        ((Action)(() => TypeSelector.From(null!, [], []))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TypeSelector.From([], [], []).Matches(null!, new TypeNames()))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TwoAssemblies_AreOred()
    {
        var a = Find(TestCompilation.Create("namespace N { public class A { } }"), "N.A");

        TypeSelector.From([], ["Other", "TestAssembly"], []).Matches(a, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void TwoTypes_AreOred()
    {
        var a = Find(TestCompilation.Create("namespace N { public class A { } }"), "N.A");

        TypeSelector.From([], [], ["N.B", "N.A"]).Matches(a, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void Matching_IsCaseSensitive()
    {
        var a = Find(TestCompilation.Create("namespace Shop.Domain { public class A { } }"), "Shop.Domain.A");

        TypeSelector.From(["shop.domain"], [], []).Matches(a, new TypeNames()).Should().BeFalse();
        TypeSelector.From([], ["testassembly"], []).Matches(a, new TypeNames()).Should().BeFalse();
        TypeSelector.From([], [], ["shop.domain.a"]).Matches(a, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void ConstructedGeneric_MatchesDefinitionName()
    {
        var compilation = TestCompilation.Create("namespace N { public class Foo<T> { } public class User { public Foo<int> F = null!; } }");
        var constructed = (INamedTypeSymbol)((IFieldSymbol)Find(compilation, "N.User").GetMembers("F")[0]).Type;

        constructed.IsDefinition.Should().BeFalse();
        TypeSelector.From([], [], ["N.Foo"]).Matches(constructed, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void NestedGeneric_MatchesWithoutArity()
    {
        var compilation = TestCompilation.Create("namespace N { public class Outer<T> { public class Inner<U> { } } }");
        var inner = Find(compilation, "N.Outer`1+Inner`1");

        TypeSelector.From([], [], ["N.Outer.Inner"]).Matches(inner, new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void MetadataType_Matches()
    {
        var library = TestCompilation.Create("namespace Lib.Core { public class Widget { } }");
        var reference = library.ToMetadataReference();
        var consumer = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("namespace App { public class User { public Lib.Core.Widget W = null!; } }")],
            [.. library.References, reference],
            library.Options);
        var widget = Find(consumer, "Lib.Core.Widget");

        TypeSelector.From(["Lib.**"], ["TestAssembly"], ["Lib.Core.Widget"]).Matches(widget, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], ["Consumer"], []).Matches(widget, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void BaseTypes_MatchesAnyClassInTheBaseChain_NotObject()
    {
        var compilation = TestCompilation.Create("namespace N { public class A { } public class B : A { } public class C : B { } }");
        var c = Find(compilation, "N.C");

        TypeSelector.From([], [], [], ["N.A"], [], []).Matches(c, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], [], ["N.B"], [], []).Matches(c, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], [], ["N.C"], [], []).Matches(c, new TypeNames()).Should().BeFalse();
        TypeSelector.From([], [], [], ["System.Object"], [], []).Matches(c, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void Interfaces_MatchesInheritedAndGenericInterfaces_ByNameWithoutArity()
    {
        var compilation = TestCompilation.Create(
            "namespace N { public interface IRepository<T> { } public abstract class RepositoryBase<T> : IRepository<T> { } public class Order { } public class OrderRepository : RepositoryBase<Order> { } }");
        var repository = Find(compilation, "N.OrderRepository");

        TypeSelector.From([], [], [], [], ["**.IRepository"], []).Matches(repository, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], [], [], ["**.IRepository`1"], []).Matches(repository, new TypeNames()).Should().BeFalse();
        TypeSelector.From([], [], [], [], ["**.IOther"], []).Matches(repository, new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void Interfaces_AnInterfaceDoesNotMatchItself()
    {
        var compilation = TestCompilation.Create("namespace N { public interface IRepository<T> { } public interface IOrders : IRepository<int> { } }");

        TypeSelector.From([], [], [], [], ["**.IRepository"], []).Matches(Find(compilation, "N.IRepository`1"), new TypeNames()).Should().BeFalse();
        TypeSelector.From([], [], [], [], ["**.IRepository"], []).Matches(Find(compilation, "N.IOrders"), new TypeNames()).Should().BeTrue();
    }

    [Fact]
    public void Attributes_MatchesTheAttributeClassFullName()
    {
        var compilation = TestCompilation.Create("namespace N { [System.Obsolete] public class Old { } public class Fresh { } }");
        var old = Find(compilation, "N.Old");

        TypeSelector.From([], [], [], [], [], ["System.ObsoleteAttribute"]).Matches(old, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], [], [], [], ["**.ObsoleteAttribute"]).Matches(old, new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], [], [], [], ["**.Obsolete"]).Matches(old, new TypeNames()).Should().BeFalse();
        TypeSelector.From([], [], [], [], [], ["**.ObsoleteAttribute"]).Matches(Find(compilation, "N.Fresh"), new TypeNames()).Should().BeFalse();
    }

    [Fact]
    public void SixListsAnd_NamespaceAndInterfaceTogether()
    {
        var compilation = TestCompilation.Create("namespace Shop.Data { public interface IRepository { } public class A : IRepository { } } namespace Other { public class B : Shop.Data.IRepository { } }");

        var selector = TypeSelector.From(["Shop.**"], [], [], [], ["**.IRepository"], []);

        selector.Matches(Find(compilation, "Shop.Data.A"), new TypeNames()).Should().BeTrue();
        selector.Matches(Find(compilation, "Other.B"), new TypeNames()).Should().BeFalse();
        selector.IsEmpty.Should().BeFalse();
        TypeSelector.From([], [], [], [], [], []).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void NullEntries_AreRejected_ForNewLists()
    {
        ((Action)(() => TypeSelector.From([], [], [], null!, [], []))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TypeSelector.From([], [], [], [], null!, []))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TypeSelector.From([], [], [], [], [], null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BaseTypes_MatchesAGenericBaseClass_ByNameWithoutArity()
    {
        var compilation = TestCompilation.Create("namespace N { public class A<T> { } public class B : A<int> { } }");

        TypeSelector.From([], [], [], ["N.A"], [], []).Matches(Find(compilation, "N.B"), new TypeNames()).Should().BeTrue();
        TypeSelector.From([], [], [], ["N.A`1"], [], []).Matches(Find(compilation, "N.B"), new TypeNames()).Should().BeFalse();
    }
}
