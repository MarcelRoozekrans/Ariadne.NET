using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class DependencyIndexTests
{
    [Fact]
    public void Dependencies_are_in_source_order_without_duplicates()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop
            {
                public class Clock { }
                public class Store { }
                public class Order
                {
                    public Store? First;
                    public Clock? Second;
                    public Store? Third;
                }
            }
            """);
        var index = Index((compilation, null));

        Names(index.DependenciesOf(Type(compilation, "Shop.Order"))).Should().Equal("Shop.Store", "Shop.Clock");
    }

    [Fact]
    public void Self_reference_is_not_a_dependency()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop
            {
                public class Store { }
                public class Order
                {
                    public Order? Next;
                    public Store? Store;
                }
            }
            """);
        var index = Index((compilation, null));

        Names(index.DependenciesOf(Type(compilation, "Shop.Order"))).Should().Equal("Shop.Store");
    }

    [Fact]
    public void Nested_type_references_belong_to_the_nested_type()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop
            {
                public class Store { }
                public class Clock { }
                public class Order
                {
                    public Clock? Clock;
                    public class Line { public Store? Store; }
                }
            }
            """);
        var index = Index((compilation, null));

        Names(index.DependenciesOf(Type(compilation, "Shop.Order"))).Should().Equal("Shop.Clock");
        Names(index.DependenciesOf(Type(compilation, "Shop.Order+Line"))).Should().Equal("Shop.Store");
    }

    [Fact]
    public void Generic_argument_is_a_dependency()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop
            {
                public interface IClock { }
                public class Order { public System.Collections.Generic.IEnumerable<IClock>? Clocks; }
            }
            """);
        var index = Index((compilation, null));

        Names(index.DependenciesOf(Type(compilation, "Shop.Order")))
            .Should().Equal("System.Collections.Generic.IEnumerable<T>", "Shop.IClock");
    }

    [Fact]
    public void Constructed_type_reads_its_original_definition()
    {
        var compilation = TestCompilation.Create("""
            namespace Shop
            {
                public class Store { }
                public class Box<T> { public Store? Store; }
                public class Order { public Box<int>? Box; }
            }
            """);
        var index = Index((compilation, null));
        var constructed = Type(compilation, "Shop.Box`1").Construct(compilation.GetSpecialType(SpecialType.System_Int32));

        Names(index.DependenciesOf(constructed)).Should().Equal("Shop.Store");
        index.IsDeclaredInRun(constructed).Should().BeTrue();
    }

    [Fact]
    public void Source_type_without_references_is_declared_in_run_with_no_dependencies()
    {
        var compilation = TestCompilation.Create("namespace Shop { public class Order { } }");
        var index = Index((compilation, null));

        index.DependenciesOf(Type(compilation, "Shop.Order")).Should().BeEmpty();
        index.IsDeclaredInRun(Type(compilation, "Shop.Order")).Should().BeTrue();
    }

    [Fact]
    public void Metadata_type_of_another_run_project_has_its_dependencies()
    {
        var a = Named("Shop.A", """
            namespace Shop.A
            {
                public class Store { }
                public class Order { public Store? Store; public class Line { public Order? Order; } }
            }
            """);
        var b = Named("Shop.B", "namespace Shop.B { public class Use { public Shop.A.Order? Order; } }", a.ToMetadataReference());
        var index = Index((a, null), (b, null));

        var order = Type(b, "Shop.A.Order");
        var line = Type(b, "Shop.A.Order+Line");

        Names(index.DependenciesOf(order)).Should().Equal("Shop.A.Store");
        Names(index.DependenciesOf(line)).Should().Equal("Shop.A.Order");
        index.IsDeclaredInRun(order).Should().BeTrue();
    }

    [Fact]
    public void Metadata_type_from_an_image_of_a_run_project_is_found_by_assembly_and_name()
    {
        var a = Named("Shop.A", "namespace Shop.A { public class Store { } public class Order { public Store? Store; } }");
        var b = Named("Shop.B", "namespace Shop.B { public class Use { public Shop.A.Order? Order; } }", Image(a));
        var index = Index((a, null), (b, null));

        var order = Type(b, "Shop.A.Order");

        Names(index.DependenciesOf(order)).Should().Equal("Shop.A.Store");
        index.DependenciesOf(order)[0].ContainingAssembly.Should().BeSameAs(a.Assembly);
        index.IsDeclaredInRun(order).Should().BeTrue();
    }

    [Fact]
    public void Type_outside_the_run_has_no_dependencies_and_is_not_declared_in_run()
    {
        var a = Named("Shop.A", "namespace Shop.A { public class Store { } public class Order { public Store? Store; } }");
        var b = Named("Shop.B", "namespace Shop.B { public class Use { public Shop.A.Order? Order; } }", Image(a));
        var index = Index((b, null));

        var text = b.GetSpecialType(SpecialType.System_String);
        index.DependenciesOf(text).Should().BeEmpty();
        index.IsDeclaredInRun(text).Should().BeFalse();

        var order = Type(b, "Shop.A.Order");
        index.DependenciesOf(order).Should().BeEmpty();
        index.IsDeclaredInRun(order).Should().BeFalse();
    }

    [Fact]
    public void Two_target_frameworks_keep_separate_dependencies()
    {
        const string source = """
            namespace Shop.A
            {
                public class Old { }
                public class New { }
                public class Order
                {
            #if NET10
                    public New? Value;
            #else
                    public Old? Value;
            #endif
                }
            }
            """;
        var a8 = Named("Shop.A", source);
        var a10 = WithSymbols(Named("Shop.A", source), "NET10");
        const string use = "namespace Shop.B { public class Use { public Shop.A.Order? Order; } }";
        var b8 = Named("Shop.B", use, a8.ToMetadataReference());
        var b10 = Named("Shop.B", use, a10.ToMetadataReference());
        var index = Index((a8, "net8.0"), (a10, "net10.0"), (b8, "net8.0"), (b10, "net10.0"));

        Names(index.DependenciesOf(Type(a8, "Shop.A.Order"))).Should().Equal("Shop.A.Old");
        Names(index.DependenciesOf(Type(a10, "Shop.A.Order"))).Should().Equal("Shop.A.New");
        Names(index.DependenciesOf(Type(b8, "Shop.A.Order"))).Should().Equal("Shop.A.Old");
        Names(index.DependenciesOf(Type(b10, "Shop.A.Order"))).Should().Equal("Shop.A.New");
    }

    [Fact]
    public void Two_target_frameworks_referenced_as_images_prefer_the_reference_of_the_same_framework()
    {
        const string source = """
            namespace Shop.A
            {
                public class Old { }
                public class New { }
                public class Order
                {
            #if NET10
                    public New? Value;
            #else
                    public Old? Value;
            #endif
                }
            }
            """;
        var a8 = Named("Shop.A", source);
        var a10 = WithSymbols(Named("Shop.A", source), "NET10");
        const string use = "namespace Shop.B { public class Use { public Shop.A.Order? Order; } }";
        var b10 = Named("Shop.B", use, Image(a10));
        var index = Index((a8, "net8.0"), (a10, "net10.0"), (b10, "net10.0"));

        Names(index.DependenciesOf(Type(b10, "Shop.A.Order"))).Should().Equal("Shop.A.New");
    }

    private static DependencyIndex Index(params (CSharpCompilation Compilation, string? Framework)[] inputs)
    {
        var projects = inputs
            .Select(i => new AnalyzedProject(i.Compilation.AssemblyName ?? string.Empty, i.Framework, i.Compilation))
            .ToList();
        var graphs = projects
            .Select(p => TypeGraphBuilder.Build(p.Compilation, new HashSet<SyntaxTree>(), CancellationToken.None))
            .ToList();
        return DependencyIndex.Build(projects, graphs);
    }

    private static INamedTypeSymbol Type(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) ?? throw new InvalidOperationException(metadataName + " not found.");

    private static List<string> Names(IReadOnlyList<INamedTypeSymbol> types) =>
        types.Select(t => t.ToDisplayString()).ToList();

    private static CSharpCompilation Named(string assembly, string source, params MetadataReference[] references) =>
        TestCompilation.Create(references, (assembly + ".cs", source)).WithAssemblyName(assembly);

    private static CSharpCompilation WithSymbols(CSharpCompilation compilation, params string[] symbols)
    {
        var tree = compilation.SyntaxTrees.First();
        var options = ((CSharpParseOptions)tree.Options).WithPreprocessorSymbols(symbols);
        return compilation.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(CSharpSyntaxTree.ParseText(tree.GetText(), options).GetRoot(), options));
    }

    private static MetadataReference Image(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException("Emit failed.");
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
