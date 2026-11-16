using Ariadne.NET.Abstractions;
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

    private const string TraitSource = """
        namespace N
        {
            public sealed class S { }
            public abstract class A { }
            public static class St { }
            public class Outer { public class Inner { } private class Hidden { } protected internal class Pi { } }
            public record R;
            public sealed record Sr;
            public record struct Rs;
            public struct Sv { }
            public enum E { One }
            public delegate void D();
            public interface I { }
            public class Base { }
            public class Impl : Base, I { }
            public class Generic<T> { }
        }
        """;

    private static readonly Lazy<CSharpCompilation> TraitCompilation = new(() => TestCompilation.Create(TraitSource));

    private static readonly string[] Classes = ["S", "A", "St", "Outer", "Inner", "Hidden", "Pi", "R", "Sr", "Base", "Impl", "Generic"];

    // The simple names of every type in namespace N, nested ones included, that the selector picks.
    private static List<string> Selected(TargetSelector selector)
    {
        var compiled = TypeSelector.From(selector);
        var names = new TypeNames();
        var selected = new List<string>();
        var pending = new Stack<INamedTypeSymbol>(TraitCompilation.Value.GlobalNamespace.GetNamespaceMembers().First(n => string.Equals(n.Name, "N", StringComparison.Ordinal)).GetTypeMembers());
        while (pending.Count > 0)
        {
            var type = pending.Pop();
            if (compiled.Matches(type, names))
            {
                selected.Add(type.Name);
            }

            foreach (var nested in type.GetTypeMembers())
            {
                pending.Push(nested);
            }
        }

        return selected;
    }

    [Fact]
    public void Names_match_the_innermost_simple_name()
    {
        Selected(new TargetSelector { Names = ["Inner"] }).Should().BeEquivalentTo("Inner");
        Selected(new TargetSelector { Names = ["Gen*"] }).Should().BeEquivalentTo("Generic");
        Selected(new TargetSelector { Names = ["Outer"] }).Should().BeEquivalentTo("Outer");
        Selected(new TargetSelector { Names = ["Generic`1"] }).Should().BeEmpty();
    }

    [Fact]
    public void Kinds_select_by_declared_kind()
    {
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Enum] }).Should().BeEquivalentTo("E");
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Delegate] }).Should().BeEquivalentTo("D");
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Record] }).Should().BeEquivalentTo("R", "Sr");
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Struct, CSharpTypeKind.RecordStruct, CSharpTypeKind.Enum] })
            .Should().BeEquivalentTo("Rs", "Sv", "E");
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Struct] }).Should().BeEquivalentTo("Sv");
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Interface] }).Should().BeEquivalentTo("I");
        Selected(new TargetSelector { Kinds = [CSharpTypeKind.Class] }).Should().BeEquivalentTo(Classes.Except(["R", "Sr"], StringComparer.Ordinal));
    }

    [Fact]
    public void Accessibility_selects_declared_accessibility()
    {
        Selected(new TargetSelector { Accessibility = ["private"] }).Should().BeEquivalentTo("Hidden");
        Selected(new TargetSelector { Accessibility = ["protected internal"] }).Should().BeEquivalentTo("Pi");
        Selected(new TargetSelector { Accessibility = ["public"] }).Should().Contain(["S", "Inner", "E", "D"]).And.NotContain(["Hidden", "Pi"]);
        Selected(new TargetSelector { Accessibility = ["internal", "private protected", "protected"] }).Should().BeEmpty();
    }

    [Fact]
    public void Unknown_accessibility_throws()
    {
        var act = () => TypeSelector.From(new TargetSelector { Accessibility = ["protected-internal"] });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignableTo_covers_self_bases_interfaces_and_object()
    {
        Selected(new TargetSelector { AssignableTo = ["**.Base"] }).Should().BeEquivalentTo("Base", "Impl");
        Selected(new TargetSelector { AssignableTo = ["**.I"] }).Should().BeEquivalentTo("I", "Impl");
        Selected(new TargetSelector { AssignableTo = ["System.Object"] }).Should().Contain(Classes).And.NotContain("I");
        Selected(new TargetSelector { AssignableTo = ["N.Generic"] }).Should().BeEquivalentTo("Generic");
    }

    [Fact]
    public void Sealed_counts_static_classes_abstract_does_not()
    {
        Selected(new TargetSelector { Sealed = true }).Should().BeEquivalentTo("S", "St", "Sr");
        Selected(new TargetSelector { Abstract = true }).Should().BeEquivalentTo("A");
        Selected(new TargetSelector { Static = true }).Should().BeEquivalentTo("St");
        Selected(new TargetSelector { Static = false }).Should().NotContain("St").And.Contain(["S", "A", "E"]);
    }

    [Fact]
    public void Non_class_kinds_are_neither_sealed_nor_abstract()
    {
        string[] others = ["I", "Sv", "Rs", "E", "D"];

        Selected(new TargetSelector { Sealed = true }).Should().NotContain(others);
        Selected(new TargetSelector { Abstract = true }).Should().NotContain(others);
        Selected(new TargetSelector { Sealed = false }).Should().Contain(others).And.NotContain(["S", "St", "Sr"]);
        Selected(new TargetSelector { Abstract = false }).Should().Contain(others).And.NotContain("A");
    }

    [Fact]
    public void Nested_selects_types_with_a_containing_type()
    {
        Selected(new TargetSelector { Nested = true }).Should().BeEquivalentTo("Inner", "Hidden", "Pi");
        Selected(new TargetSelector { Nested = false }).Should().NotContain(["Inner", "Hidden", "Pi"]).And.Contain("Outer");
    }

    [Fact]
    public void Traits_and_globs_are_anded()
    {
        Selected(new TargetSelector { Types = ["N.Outer.*"], Accessibility = ["public"] }).Should().BeEquivalentTo("Inner");
        Selected(new TargetSelector { Interfaces = ["**.I"], Sealed = true }).Should().BeEmpty();
    }

    [Fact]
    public void Trait_only_selector_is_not_empty()
    {
        TypeSelector.From(new TargetSelector()).IsEmpty.Should().BeTrue();
        TypeSelector.From(new TargetSelector { Names = ["X"] }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Kinds = [CSharpTypeKind.Class] }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Accessibility = ["public"] }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { AssignableTo = ["**.I"] }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Sealed = false }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Abstract = true }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Static = true }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Nested = false }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { Members = ["A.B.C"] }).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void KindOf_maps_enum_and_delegate_and_leaves_ActualKind_alone()
    {
        var compilation = TraitCompilation.Value;

        TypeTraits.KindOf(Find(compilation, "N.E")).Should().Be(CSharpTypeKind.Enum);
        TypeTraits.KindOf(Find(compilation, "N.D")).Should().Be(CSharpTypeKind.Delegate);
        TypeTraits.KindOf(Find(compilation, "N.Rs")).Should().Be(CSharpTypeKind.RecordStruct);
        RoleRules.ActualKind(Find(compilation, "N.E")).Should().Be(CSharpTypeKind.Class);
        RoleRules.ActualKind(Find(compilation, "N.D")).Should().Be(CSharpTypeKind.Class);
    }

    private const string AttributeSource = """
        namespace N
        {
            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
            public sealed class RouteAttribute : System.Attribute
            {
                public RouteAttribute(string path) { }
                public RouteAttribute(string path, int version) { }
                public int Version { get; set; }
            }

            public sealed class TagAttribute : System.Attribute { public TagAttribute(string name) { } }
            public sealed class OtherAttribute : System.Attribute { }

            public class Plain { }
            [Route("api/orders", 2, Version = 3)] [Tag("x")] public class Orders { }
            [Route("api/a")] [Route("api/b")] public class Split { }
            [Tag("x")] [Other] public class Mixed { }
            [Tag("x")] [Other] [Route("r")] public class Many { }
        }
        """;

    private static readonly Lazy<CSharpCompilation> AttributeCompilation = new(() => TestCompilation.Create(AttributeSource));

    private static bool AttributeSelects(TargetSelector selector, string metadataName) =>
        TypeSelector.From(selector).Matches(Find(AttributeCompilation.Value, metadataName), new TypeNames());

    [Fact]
    public void OnlyAttributes_matches_type_without_attributes()
    {
        AttributeSelects(new TargetSelector { OnlyAttributes = ["N.TagAttribute"] }, "N.Plain").Should().BeTrue();
    }

    [Fact]
    public void OnlyAttributes_matches_when_all_inside()
    {
        AttributeSelects(new TargetSelector { OnlyAttributes = ["N.RouteAttribute", "N.TagAttribute"] }, "N.Orders").Should().BeTrue();
        AttributeSelects(new TargetSelector { OnlyAttributes = ["N.*Attribute"] }, "N.Orders").Should().BeTrue();
        AttributeSelects(new TargetSelector { OnlyAttributes = ["N.RouteAttribute"] }, "N.Split").Should().BeTrue();
    }

    [Fact]
    public void OnlyAttributes_fails_when_one_outside()
    {
        AttributeSelects(new TargetSelector { OnlyAttributes = ["N.TagAttribute"] }, "N.Mixed").Should().BeFalse();
        AttributeSelects(new TargetSelector { OnlyAttributes = ["N.TagAttribute"] }, "N.Orders").Should().BeFalse();
    }

    [Fact]
    public void OnlyAttributes_ignores_compiler_attributes_on_metadata_type()
    {
        var library = TestCompilation.Create("""
            namespace Lib
            {
                public sealed class TagAttribute : System.Attribute { }
                [Tag] public class Holder { public string? First; public string? Second; public object? Third; }
                public class Bare { public string? First; public string? Second; public object? Third; }
            }
            """);
        using var image = new MemoryStream();
        library.Emit(image).Success.Should().BeTrue();
        var consumer = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("namespace App { public class User { public Lib.Holder? H; public Lib.Bare? B; } }")],
            [.. library.References, MetadataReference.CreateFromImage(image.ToArray())],
            library.Options);
        var holder = Find(consumer, "Lib.Holder");
        var bare = Find(consumer, "Lib.Bare");
        var names = new TypeNames();

        // The precondition this test exists for: the metadata type carries a compiler-emitted attribute.
        bare.GetAttributes().Should().NotBeEmpty();
        bare.GetAttributes().Should().OnlyContain(a => TypeSelector.IsCompilerAttribute(a.AttributeClass!, names));

        var selector = TypeSelector.From(new TargetSelector { OnlyAttributes = ["Lib.TagAttribute"] });
        selector.Matches(holder, names).Should().BeTrue();
        selector.Matches(bare, names).Should().BeTrue();
        selector.FirstOutsideOnlyAttributes(holder, names).Should().BeNull();
        TypeSelector.From(new TargetSelector { OnlyAttributes = ["Other.*"] }).Matches(holder, names).Should().BeFalse();
    }

    [Fact]
    public void OnlyAttributes_ignores_default_member_attribute_on_metadata_type_with_indexer()
    {
        var library = TestCompilation.Create("""
            namespace Lib
            {
                public sealed class TagAttribute : System.Attribute { }
                [Tag] public class Indexed { public int this[int index] => index; }
            }
            """);
        using var image = new MemoryStream();
        library.Emit(image).Success.Should().BeTrue();
        var consumer = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText("namespace App { public class User { public Lib.Indexed? I; } }")],
            [.. library.References, MetadataReference.CreateFromImage(image.ToArray())],
            library.Options);
        var indexed = Find(consumer, "Lib.Indexed");
        var names = new TypeNames();

        // The precondition this test exists for: the metadata type with an indexer carries DefaultMemberAttribute.
        indexed.GetAttributes().Should().Contain(a => names.FullNameOf(a.AttributeClass!) == "System.Reflection.DefaultMemberAttribute");
        Find(library, "Lib.Indexed").GetAttributes().Should().NotContain(a => names.FullNameOf(a.AttributeClass!) == "System.Reflection.DefaultMemberAttribute");

        var selector = TypeSelector.From(new TargetSelector { OnlyAttributes = ["Lib.TagAttribute"] });
        selector.Matches(indexed, names).Should().BeTrue();
        selector.FirstOutsideOnlyAttributes(indexed, names).Should().BeNull();
    }

    [Fact]
    public void IsCompilerAttribute_holds_the_ignore_list()
    {
        string[] ignored =
        [
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
            "System.Runtime.CompilerServices.NullableAttribute",
            "System.Runtime.CompilerServices.NullableContextAttribute",
            "System.Runtime.CompilerServices.NullablePublicOnlyAttribute",
            "System.Runtime.CompilerServices.IsReadOnlyAttribute",
            "System.Runtime.CompilerServices.IsByRefLikeAttribute",
            "System.Reflection.DefaultMemberAttribute",
            "Microsoft.CodeAnalysis.EmbeddedAttribute",
        ];

        // Each class is declared in source, so the lookup below always finds it and every list member is checked.
        var source = string.Concat(ignored.Select(name =>
        {
            var dot = name.LastIndexOf('.');
            return $"namespace {name[..dot]} {{ internal sealed class {name[(dot + 1)..]} : System.Attribute {{ }} }}\n";
        }));
        var declared = TestCompilation.Create(source);
        var compilation = AttributeCompilation.Value;
        var names = new TypeNames();

        foreach (var name in ignored)
        {
            var symbol = Find(declared, name);
            symbol.Locations.Should().Contain(l => l.IsInSource, name);
            TypeSelector.IsCompilerAttribute(symbol, names).Should().BeTrue(name);
        }

        TypeSelector.IsCompilerAttribute(Find(compilation, "N.TagAttribute"), names).Should().BeFalse();
        TypeSelector.IsCompilerAttribute(Find(compilation, "System.ObsoleteAttribute"), names).Should().BeFalse();
    }

    [Fact]
    public void AttributeArguments_does_not_match_type_without_attributes()
    {
        AttributeSelects(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["x"] }] }, "N.Plain").Should().BeFalse();
        AttributeSelects(
            new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Named = new Dictionary<string, string>(StringComparer.Ordinal) { ["Version"] = "3" } }] },
            "N.Plain").Should().BeFalse();
    }

    [Fact]
    public void AttributeArguments_entries_are_ored()
    {
        var entries = new TargetSelector
        {
            AttributeArguments =
            [
                new AttributeArgumentMatch { Arguments = ["nope"] },
                new AttributeArgumentMatch { Attribute = "N.TagAttribute", Arguments = ["x"] },
            ],
        };

        AttributeSelects(entries, "N.Orders").Should().BeTrue();
        AttributeSelects(entries, "N.Split").Should().BeFalse();
        AttributeSelects(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["nope"] }] }, "N.Orders").Should().BeFalse();
    }

    [Fact]
    public void AttributeArguments_entry_needs_one_instance_holding_every_value()
    {
        AttributeSelects(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["api/a", "api/b"] }] }, "N.Split").Should().BeFalse();
        AttributeSelects(
            new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["api/a"] }, new AttributeArgumentMatch { Arguments = ["api/b"] }] },
            "N.Split").Should().BeTrue();
        AttributeSelects(
            new TargetSelector
            {
                AttributeArguments =
                [
                    new AttributeArgumentMatch
                    {
                        Attribute = "N.RouteAttribute",
                        Arguments = ["2", "api/orders"],
                        Named = new Dictionary<string, string>(StringComparer.Ordinal) { ["Version"] = "3" },
                    },
                ],
            },
            "N.Orders").Should().BeTrue();
    }

    [Fact]
    public void Both_keys_and_attributes_are_anded()
    {
        var selector = new TargetSelector
        {
            Attributes = ["N.RouteAttribute"],
            AttributeArguments = [new AttributeArgumentMatch { Arguments = ["x"] }],
            OnlyAttributes = ["N.RouteAttribute", "N.TagAttribute"],
        };

        AttributeSelects(selector, "N.Orders").Should().BeTrue();
        AttributeSelects(selector, "N.Mixed").Should().BeFalse();
        AttributeSelects(selector, "N.Many").Should().BeFalse();
        AttributeSelects(selector, "N.Split").Should().BeFalse();
        AttributeSelects(
            new TargetSelector { Attributes = ["N.RouteAttribute"], AttributeArguments = [new AttributeArgumentMatch { Arguments = ["x"] }], OnlyAttributes = ["N.RouteAttribute"] },
            "N.Orders").Should().BeFalse();
        AttributeSelects(
            new TargetSelector { Namespaces = ["Other.**"], AttributeArguments = [new AttributeArgumentMatch { Arguments = ["x"] }] },
            "N.Orders").Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_is_false_with_only_attribute_arguments()
    {
        TypeSelector.From(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["x"] }] }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new TargetSelector { OnlyAttributes = ["N.*"] }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new RuleException { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["x"] }], Because = "B." }).IsEmpty.Should().BeFalse();
        TypeSelector.From(new RuleException { OnlyAttributes = ["N.*"], Because = "B." }).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void FirstOutsideOnlyAttributes_names_first_in_declaration_order()
    {
        var names = new TypeNames();
        var selector = TypeSelector.From(new TargetSelector { OnlyAttributes = ["N.TagAttribute"] });

        selector.FirstOutsideOnlyAttributes(Find(AttributeCompilation.Value, "N.Many"), names).Should().Be("N.OtherAttribute");
        selector.FirstOutsideOnlyAttributes(Find(AttributeCompilation.Value, "N.Orders"), names).Should().Be("N.RouteAttribute");
        selector.FirstOutsideOnlyAttributes(Find(AttributeCompilation.Value, "N.Plain"), names).Should().BeNull();
        TypeSelector.From(new TargetSelector { OnlyAttributes = ["N.*"] }).FirstOutsideOnlyAttributes(Find(AttributeCompilation.Value, "N.Many"), names).Should().BeNull();
    }

    [Fact]
    public void Attribute_matching_allocates_nothing_once_names_are_built()
    {
        var orders = Find(AttributeCompilation.Value, "N.Orders");
        var names = new TypeNames();
        var selector = TypeSelector.From(new TargetSelector
        {
            Attributes = ["N.RouteAttribute"],
            AttributeArguments = [new AttributeArgumentMatch { Attribute = "N.TagAttribute", Arguments = ["x"] }],
            OnlyAttributes = ["N.RouteAttribute", "N.TagAttribute"],
        });
        selector.Matches(orders, names).Should().BeTrue();

        Allocations.PerRound(() =>
        {
            selector.Matches(orders, names);
        }).Should().Be(0);
    }

    [Fact]
    public void Trait_matching_allocates_nothing_once_names_are_built()
    {
        var impl = Find(TraitCompilation.Value, "N.Impl");
        var names = new TypeNames();
        var selector = TypeSelector.From(new TargetSelector
        {
            Names = ["Im*"],
            Kinds = [CSharpTypeKind.Class],
            Accessibility = ["public"],
            AssignableTo = ["**.I"],
            Sealed = false,
            Abstract = false,
            Static = false,
            Nested = false,
        });
        selector.Matches(impl, names).Should().BeTrue();

        Allocations.PerRound(() =>
        {
            selector.Matches(impl, names);
        }).Should().Be(0);
    }
}
