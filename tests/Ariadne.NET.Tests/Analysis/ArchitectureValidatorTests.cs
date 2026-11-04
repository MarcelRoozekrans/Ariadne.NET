using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class ArchitectureValidatorTests
{
    private static readonly RulePack Pack = new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", AllowedDependencies = [] },
            new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
        ],
        ComponentRoles =
        [
            new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record },
            new ComponentRoleDefinition { Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
            new ComponentRoleDefinition { Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface, AlternativeNames = ["Repository"] },
        ],
    };

    private const string BrokenCommand = "namespace MyApp.Orders.Application; public class CreateOrderCommand { }";

    private readonly ArchitectureValidator _validator = new();

    [Fact]
    public void SingleCompilation_CleanAndBroken()
    {
        var clean = _validator.Validate(Pack, TestCompilation.Create("namespace MyApp.Orders.Application; public record CreateOrderCommand;"));
        clean.IsValid.Should().BeTrue();
        clean.TypesChecked.Should().Be(1);
        var project = clean.Projects.Should().ContainSingle().Subject;
        project.Name.Should().Be("TestAssembly");
        project.TargetFrameworks.Should().BeEmpty();

        var broken = _validator.Validate(Pack, TestCompilation.Create(BrokenCommand));
        broken.IsValid.Should().BeFalse();
        var violation = broken.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.TypeKindMismatch);
        violation.RuleId.Should().Be("role/Command");
        violation.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void TwoTargetFrameworks_SameViolation_ReportedOnceWithEmptyList()
    {
        var report = _validator.Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", TestCompilation.Create(BrokenCommand)),
            new AnalyzedProject("App", "net10.0", TestCompilation.Create(BrokenCommand)),
        ]);

        report.Violations.Should().ContainSingle().Which.TargetFrameworks.Should().BeEmpty();
        report.Projects.Should().ContainSingle().Which.Should().BeEquivalentTo(new CheckedProject("App", ["net10.0", "net8.0"]), o => o.WithStrictOrdering());
        report.TypesChecked.Should().Be(1);
    }

    [Fact]
    public void ViolationInOneTargetFramework_ListsIt()
    {
        const string source = """
            namespace MyApp.Orders.Application;
            #if OLD
            public class CreateOrderCommand { }
            #else
            public record CreateOrderCommand;
            #endif
            """;

        var report = _validator.Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", Compile(source, "OLD")),
            new AnalyzedProject("App", "net10.0", Compile(source)),
        ]);

        report.Violations.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net8.0");
    }

    [Fact]
    public void ViolationInSomeTargetFrameworks_KeepsEarliestLocationAndHighestCount()
    {
        const string source = """
            namespace MyApp.Orders.Domain;
            public class Order
            {
            #if MANY
                MyApp.Orders.Infrastructure.Db _a;
                MyApp.Orders.Infrastructure.Db _b;
            #else
                private int _x;
                MyApp.Orders.Infrastructure.Db _a;
            #endif
            }
            namespace MyApp.Orders.Infrastructure { public class Db { } }
            """;
        var withMany = Compile(source.Replace("namespace MyApp.Orders.Domain;", "namespace MyApp.Orders.Domain {").Replace("namespace MyApp.Orders.Infrastructure {", "} namespace MyApp.Orders.Infrastructure {"), "MANY");
        var without = Compile(source.Replace("namespace MyApp.Orders.Domain;", "namespace MyApp.Orders.Domain {").Replace("namespace MyApp.Orders.Infrastructure {", "} namespace MyApp.Orders.Infrastructure {"));

        var report = _validator.Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", withMany),
            new AnalyzedProject("App", "net10.0", without),
        ]);

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.DependencyNotAllowed);
        violation.ReferenceCount.Should().Be(2);
        violation.TargetFrameworks.Should().BeEmpty();
        violation.Location!.Value.Line.Should().Be(5);
    }

    [Fact]
    public void TwoProjectsSameTypeName_NotMerged()
    {
        var report = _validator.Validate(Pack,
        [
            new AnalyzedProject("A", null, TestCompilation.Create(BrokenCommand)),
            new AnalyzedProject("B", null, TestCompilation.Create(BrokenCommand)),
        ]);

        report.Violations.Should().HaveCount(2);
        report.Projects.Select(p => p.Name).Should().Equal("A", "B");
        report.Projects.Should().OnlyContain(p => p.TargetFrameworks.Count == 0);
        report.TypesChecked.Should().Be(2);
    }

    [Fact]
    public void TypesChecked_CountsSourceTypes()
    {
        const string source = """
            namespace MyApp.Orders.Domain;
            public class Order { public class Line { } }
            public enum Status { A }
            public interface IThing { }
            """;
        var report = _validator.Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", TestCompilation.Create(source)),
            new AnalyzedProject("App", "net10.0", TestCompilation.Create(source)),
            new AnalyzedProject("Other", null, TestCompilation.Create("namespace X; public class Y { }")),
        ]);

        report.TypesChecked.Should().Be(5);
    }

    [Fact]
    public void Violations_AreOrdered()
    {
        var compilation = TestCompilation.Create(
            ("B.cs", "namespace MyApp.Orders.Application; public class BCommand { }"),
            ("A.cs", "namespace MyApp.Orders.Application; public class ACommand { }"),
            ("C.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }"));

        var report = _validator.Validate(Pack, compilation);

        report.Violations.Select(v => (v.Location?.FilePath, v.Kind)).Should().Equal(
            ("A.cs", ViolationKind.TypeKindMismatch),
            ("B.cs", ViolationKind.TypeKindMismatch),
            (null, ViolationKind.MissingInterface));
    }

    [Fact]
    public void MissingInterface_IsSatisfiedByAnotherProjectOfTheSameTargetFramework()
    {
        var repository = ("Repo.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }");
        var contract = ("Contract.cs", "namespace MyApp.Orders.Domain; public interface IOrderRepository { }");

        _validator.Validate(Pack,
        [
            new AnalyzedProject("Infra", "net8.0", TestCompilation.Create(repository)),
            new AnalyzedProject("Domain", "net8.0", TestCompilation.Create(contract)),
        ]).IsValid.Should().BeTrue();

        _validator.Validate(Pack,
        [
            new AnalyzedProject("Infra", null, TestCompilation.Create(repository)),
            new AnalyzedProject("Domain", null, TestCompilation.Create(contract)),
        ]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingInterface_IsNotSatisfiedByAnotherTargetFramework()
    {
        var repository = ("Repo.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }");
        var contract = ("Contract.cs", "namespace MyApp.Orders.Domain; public interface IOrderRepository { }");

        var report = _validator.Validate(Pack,
        [
            new AnalyzedProject("Infra", "net8.0", TestCompilation.Create(repository)),
            new AnalyzedProject("Domain", "net10.0", TestCompilation.Create(contract)),
        ]);

        report.Violations.Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.MissingInterface);
    }

    [Fact]
    public void NoProjects_IsValidAndEmpty()
    {
        var report = _validator.Validate(Pack, []);

        report.IsValid.Should().BeTrue();
        report.Projects.Should().BeEmpty();
        report.TypesChecked.Should().Be(0);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var compilation = TestCompilation.Create("class C { }");
        IReadOnlyList<AnalyzedProject> projects = [];

        ((Action)(() => _validator.Validate(null!, compilation))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _validator.Validate(Pack, (Compilation)null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _validator.Validate(null!, projects))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _validator.Validate(Pack, (IReadOnlyList<AnalyzedProject>)null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _validator.Validate(Pack, [null!]))).Should().Throw<ArgumentNullException>();
    }

    private static CSharpCompilation Compile(string source, params string[] symbols)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(preprocessorSymbols: symbols),
            path: "Test.cs");
        return CSharpCompilation.Create(
            "TestAssembly",
            [tree],
            TestCompilation.Create("class C { }").References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
