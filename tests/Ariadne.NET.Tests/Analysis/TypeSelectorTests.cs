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

        TypeSelector.From(["**.Domain.**"], [], []).Matches(order).Should().BeTrue();
        TypeSelector.From(["**.Infrastructure.**"], [], []).Matches(order).Should().BeFalse();
    }

    [Fact]
    public void Assembly_Matches()
    {
        var compilation = TestCompilation.Create("namespace N { public class A { } }");
        var a = Find(compilation, "N.A");

        TypeSelector.From([], ["TestAssembly"], []).Matches(a).Should().BeTrue();
        TypeSelector.From([], ["Other*"], []).Matches(a).Should().BeFalse();
    }

    [Fact]
    public void Type_MatchesFullNameWithoutArity()
    {
        var compilation = TestCompilation.Create("namespace N { public class Foo<T> { } }");
        var foo = Find(compilation, "N.Foo`1");

        TypeSelector.From([], [], ["N.Foo"]).Matches(foo).Should().BeTrue();
        TypeSelector.From([], [], ["N.Foo`1"]).Matches(foo).Should().BeFalse();
    }

    [Fact]
    public void NestedType_MatchesOuterDotInner()
    {
        var compilation = TestCompilation.Create("public class Outer { public class Inner { } }");
        var inner = Find(compilation, "Outer+Inner");

        TypeSelector.From([], [], ["Outer.Inner"]).Matches(inner).Should().BeTrue();
        TypeSelector.From([], [], ["**.Inner"]).Matches(inner).Should().BeTrue();
        TypeSelector.From([], [], ["Inner"]).Matches(inner).Should().BeFalse();
    }

    [Fact]
    public void NestedTypeInNamespace_MatchesNamespaceOuterInner()
    {
        var compilation = TestCompilation.Create("namespace N { public class Outer { public class Inner { } } }");
        var inner = Find(compilation, "N.Outer+Inner");

        TypeSelector.From([], [], ["N.Outer.Inner"]).Matches(inner).Should().BeTrue();
        TypeSelector.From([], [], ["N.Inner"]).Matches(inner).Should().BeFalse();
    }

    [Fact]
    public void GlobalNamespaceType_MatchesDoubleStar()
    {
        var compilation = TestCompilation.Create("public class Loose { }");
        var loose = Find(compilation, "Loose");

        TypeSelector.From(["**"], [], []).Matches(loose).Should().BeTrue();
        TypeSelector.From(["Shop"], [], []).Matches(loose).Should().BeFalse();
        TypeSelector.From([], [], ["Loose"]).Matches(loose).Should().BeTrue();
    }

    [Fact]
    public void AssemblyAndNamespace_AreAnded()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class A { } }");
        var a = Find(compilation, "Shop.Domain.A");

        TypeSelector.From(["Shop.**"], ["TestAssembly"], []).Matches(a).Should().BeTrue();
        TypeSelector.From(["Shop.**"], ["Other"], []).Matches(a).Should().BeFalse();
        TypeSelector.From(["Other.**"], ["TestAssembly"], []).Matches(a).Should().BeFalse();
    }

    [Fact]
    public void TwoNamespaces_AreOred()
    {
        var compilation = TestCompilation.Create("namespace Shop.Domain { public class A { } }");
        var a = Find(compilation, "Shop.Domain.A");

        TypeSelector.From(["Other", "Shop.Domain"], [], []).Matches(a).Should().BeTrue();
    }

    [Fact]
    public void EmptySelector_MatchesEverything()
    {
        var compilation = TestCompilation.Create("namespace N { public class A { } }");

        TypeSelector.From([], [], []).Matches(Find(compilation, "N.A")).Should().BeTrue();
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
        var compilation = TestCompilation.Create("public class A { }");

        ((Action)(() => TypeSelector.From(null!, [], []))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TypeSelector.From([], [], []).Matches(null!))).Should().Throw<ArgumentNullException>();
        Find(compilation, "A").Should().NotBeNull();
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

        TypeSelector.From(["Lib.**"], ["TestAssembly"], ["Lib.Core.Widget"]).Matches(widget).Should().BeTrue();
        TypeSelector.From([], ["Consumer"], []).Matches(widget).Should().BeFalse();
    }
}
