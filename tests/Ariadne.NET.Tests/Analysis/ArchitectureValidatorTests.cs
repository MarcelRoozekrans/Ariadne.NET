using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
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
            new LayerDefinition { Name = "Domain", Id = "TEST-LAYER-DOMAIN", Because = "Test layer Domain.", Namespaces = ["**.Domain.**"], AllowedDependencies = [] },
            new LayerDefinition { Name = "Application", Id = "TEST-LAYER-APPLICATION", Because = "Test layer Application.", Namespaces = ["**.Application.**"], AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", Id = "TEST-LAYER-INFRASTRUCTURE", Because = "Test layer Infrastructure.", Namespaces = ["**.Infrastructure.**"], AllowedDependencies = ["Domain", "Application"] },
        ],
        ComponentRoles =
        [
            new ComponentRoleDefinition { AllowEmpty = true, Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record },
            new ComponentRoleDefinition { AllowEmpty = true, Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
            new ComponentRoleDefinition { AllowEmpty = true, Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface, AlternativeNames = ["Repository"] },
        ],
    };

    // The fixture pack above has three layers. A test that does not cover all of them validates against the layers it covers.
    private static RulePack PackOf(params string[] layers) => new()
    {
        Name = Pack.Name,
        Layers = Pack.Layers.Where(l => Array.IndexOf(layers, l.Name) >= 0).ToList(),
        ComponentRoles = Pack.ComponentRoles.Where(r => Array.IndexOf(layers, r.Layer) >= 0).ToList(),
    };

    private static readonly RulePack ApplicationOnly = PackOf("Application");

    private const string BrokenCommand = "namespace MyApp.Orders.Application; public class CreateOrderCommand { }";

    private readonly ArchitectureValidator _validator = new();

    [Fact]
    public void SingleCompilation_CleanAndBroken()
    {
        var clean = _validator.Validate(ApplicationOnly, TestCompilation.Create("namespace MyApp.Orders.Application; public record CreateOrderCommand;"));
        clean.IsValid.Should().BeTrue();
        clean.TypesChecked.Should().Be(1);
        var project = clean.Projects.Should().ContainSingle().Subject;
        project.Name.Should().Be("TestAssembly");
        project.TargetFrameworks.Should().BeEmpty();

        var broken = _validator.Validate(ApplicationOnly, TestCompilation.Create(BrokenCommand));
        broken.IsValid.Should().BeFalse();
        var violation = broken.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.TypeKindMismatch);
        violation.RuleId.Should().Be("ROLE-Command");
        violation.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void TwoTargetFrameworks_SameViolation_ReportedOnceWithEmptyList()
    {
        var report = _validator.Validate(ApplicationOnly,
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

        var report = _validator.Validate(ApplicationOnly,
        [
            new AnalyzedProject("App", "net8.0", Compile(source, "OLD")),
            new AnalyzedProject("App", "net10.0", Compile(source)),
        ]);

        report.Violations.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net8.0");
    }

    [Fact]
    public void MergedViolation_KeepsEarliestLocationFromOneFrameworkAndHighestCountFromAnother()
    {
        // net8.0 sees one reference at line 7. net10.0 sees two references, the first at line 11.
        const string source = """
            namespace MyApp.Orders.Infrastructure { public class Db { } }
            namespace MyApp.Orders.Domain
            {
                public class Order
                {
            #if SINGLE
                    MyApp.Orders.Infrastructure.Db _a;
            #else
                    int _pad;
                    int _pad2;
                    MyApp.Orders.Infrastructure.Db _a;
                    MyApp.Orders.Infrastructure.Db _b;
            #endif
                }
            }
            """;

        var report = _validator.Validate(PackOf("Domain", "Infrastructure"),
        [
            new AnalyzedProject("App", "net8.0", Compile(source, "SINGLE")),
            new AnalyzedProject("App", "net10.0", Compile(source)),
        ]);

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.DependencyNotAllowed);
        violation.Location.Should().Be(new SourceLocation("Test.cs", 7, 37));
        violation.ReferenceCount.Should().Be(2);
        violation.Message.Should().Be(
            "Layer 'Domain' is not allowed to depend on 'Infrastructure': MyApp.Orders.Domain.Order references MyApp.Orders.Infrastructure.Db 2 times");
        violation.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void ProjectMixingNullAndNamedTargetFrameworks_IsRejected()
    {
        var compilation = TestCompilation.Create(BrokenCommand);

        var act = () => _validator.Validate(Pack,
        [
            new AnalyzedProject("Mixed", null, compilation),
            new AnalyzedProject("Mixed", "net8.0", compilation),
        ]);

        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("Mixed");
    }

    [Fact]
    public void TwoProjectsSameTypeName_NotMerged()
    {
        var report = _validator.Validate(ApplicationOnly,
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
            ("C.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }"),
            ("D.cs", "namespace MyApp.Orders.Domain; public class Order { }"));

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

        _validator.Validate(PackOf("Domain", "Infrastructure"),
        [
            new AnalyzedProject("Infra", "net8.0", TestCompilation.Create(repository)),
            new AnalyzedProject("Domain", "net8.0", TestCompilation.Create(contract)),
        ]).IsValid.Should().BeTrue();

        _validator.Validate(PackOf("Domain", "Infrastructure"),
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

        var report = _validator.Validate(PackOf("Domain", "Infrastructure"),
        [
            new AnalyzedProject("Infra", "net8.0", TestCompilation.Create(repository)),
            new AnalyzedProject("Domain", "net10.0", TestCompilation.Create(contract)),
        ]);

        report.Violations.Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.MissingInterface);
    }

    [Fact]
    public void MissingInterface_SingleTargetInterfaceSatisfiesMultiTargetImplementation()
    {
        var repository = ("Repo.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }");
        var contract = ("Contract.cs", "namespace MyApp.Orders.Domain; public interface IOrderRepository { }");

        // Workspaces gives a single-target project a null framework and each framework of a multi-target project its name.
        _validator.Validate(PackOf("Domain", "Infrastructure"),
        [
            new AnalyzedProject("Domain", null, TestCompilation.Create(contract)),
            new AnalyzedProject("Infra", "netstandard2.0", TestCompilation.Create(repository)),
            new AnalyzedProject("Infra", "net8.0", TestCompilation.Create(repository)),
        ]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingInterface_MultiTargetInterfaceSatisfiesSingleTargetImplementation()
    {
        var repository = ("Repo.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }");
        var contract = ("Contract.cs", "namespace MyApp.Orders.Domain; public interface IOrderRepository { }");

        _validator.Validate(PackOf("Domain", "Infrastructure"),
        [
            new AnalyzedProject("Domain", "netstandard2.0", TestCompilation.Create(contract)),
            new AnalyzedProject("Domain", "net8.0", TestCompilation.Create(contract)),
            new AnalyzedProject("Infra", null, TestCompilation.Create(repository)),
        ]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void NoProjects_IsValidAndEmpty()
    {
        var report = _validator.Validate(new RulePack { Name = "test" }, []);

        report.IsValid.Should().BeTrue();
        report.Projects.Should().BeEmpty();
        report.TypesChecked.Should().Be(0);
    }

    [Fact]
    public void RetargetIntoOverlap_ReportsLayerOverlap()
    {
        var loader = new YamlRulePackLoader();
        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var child = new RulePack
        {
            Name = "child",
            Extends = ["builtin://clean-architecture"],
            LayerRetargets = [new LayerRetarget { Name = "Application", Namespaces = ["**.Domain.**"] }],
        };
        var resolved = resolver.Resolve(child);
        resolved.IsSuccess.Should().BeTrue();

        var report = _validator.Validate(
            resolved.Value,
            TestCompilation.Create("""
                namespace Shop.Domain { public class Order { } }
                namespace Shop.Infrastructure { public class Store { } }
                namespace Shop.WebApi { public class Endpoint { } }
                """));

        var v = report.Violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.LayerOverlap);
        v.RuleId.Should().Be("CA-LAYER-DOMAIN+CA-LAYER-APPLICATION");
        v.SourceType.Should().Be("Shop.Domain.Order");
        v.Message.Should().Be("Type 'Shop.Domain.Order' is covered by layers 'Domain', 'Application'");
    }

    [Fact]
    public void Validate_RunsRequireRules_AndLayerOverlapOnceForAnOverlappedType()
    {
        var pack = new RulePack
        {
            Name = "test",
            Layers =
            [
                new LayerDefinition { Name = "Domain", Id = "L1", Because = "b", Namespaces = ["**.Domain.**"] },
                new LayerDefinition { Name = "Application", Id = "L2", Because = "b", Namespaces = ["**.Application.**"] },
            ],
            Rules =
            [
                new RuleDefinition
                {
                    Id = "TEST-REQUIRE",
                    Because = "Orders live in Application.",
                    Kind = RuleKind.Require,
                    From = new TargetSelector { Types = ["**.*Order"] },
                    Require = new RuleRequirement { Layer = "Application" },
                },
            ],
        };

        var report = _validator.Validate(
            pack,
            TestCompilation.Create("""
                namespace Shop.Domain { public class Order { } }
                namespace Shop.Domain.Application { public class BigOrder { } }
                """));

        report.Violations.Should().HaveCount(2);
        report.Violations.Should().Contain(v => v.Kind == ViolationKind.WrongLayer && v.SourceType == "Shop.Domain.Order" && v.RuleId == "TEST-REQUIRE");
        report.Violations.Should().Contain(v => v.Kind == ViolationKind.LayerOverlap && v.SourceType == "Shop.Domain.Application.BigOrder");
    }

    [Fact]
    public void OverlappedType_ReferencedByTwoOtherProjects_ReportedOnceWithLocation()
    {
        var references = TestCompilation.Create("class C { }").References.ToList();
        var core = CSharpCompilation.Create(
            "Core",
            [CSharpSyntaxTree.ParseText("namespace Shop.Domain.Application { public class Order { } }", path: "Order.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        CSharpCompilation User(string name) => CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText($"namespace {name}.Web {{ public class Page {{ public Shop.Domain.Application.Order? Order; }} }}", path: $"{name}.cs")],
            [.. references, core.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var report = _validator.Validate(PackOf("Domain", "Application"),
        [
            new AnalyzedProject("Core", null, core),
            new AnalyzedProject("B", null, User("B")),
            new AnalyzedProject("C", null, User("C")),
        ]);

        var v = report.Violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.LayerOverlap);
        v.SourceType.Should().Be("Shop.Domain.Application.Order");
        v.Location.Should().NotBeNull();
        v.Location!.Value.FilePath.Should().Be("Order.cs");
    }

    [Fact]
    public void OverlappedMetadataType_ReferencedByTwoProjects_ReportedOnce()
    {
        var references = TestCompilation.Create("class C { }").References.ToList();
        var library = CSharpCompilation.Create(
            "Library",
            [CSharpSyntaxTree.ParseText("namespace Shop.Domain.Application { public class Order { } }")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        CSharpCompilation User(string name) => CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText($"namespace {name}.Web {{ public class Page {{ public Shop.Domain.Application.Order? Order; }} }}", path: $"{name}.cs")],
            [.. references, library.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var report = _validator.Validate(PackOf("Domain", "Application"),
        [
            new AnalyzedProject("B", null, User("B")),
            new AnalyzedProject("C", null, User("C")),
        ]);

        var v = report.Violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.LayerOverlap);
        v.Location.Should().BeNull();
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

    [Fact]
    public void DependencyRules_TwoFrameworks_MergeToOneViolationWithHighestCount()
    {
        const string source = """
            namespace Web { public class Client { } }
            namespace Hosts { public class Host { } }
            namespace Shop.Domain
            {
                public class Order
                {
                    public Web.Client? A;
            #if MANY
                    public Web.Client? B;
                    public Web.Client? C;
            #endif
                }
            }
            """;
        var pack = new RulePack
        {
            Name = "test",
            Layers = [new LayerDefinition { Name = "Domain", Id = "L-DOMAIN", Because = "Domain.", Namespaces = ["**.Domain.**"] }],
            Rules =
            [
                new RuleDefinition { Id = "R-FORBID", Because = "No web.", Kind = RuleKind.Forbid, From = new TargetSelector { Layers = ["Domain"] }, To = new TargetSelector { Namespaces = ["Web"] } },
                new RuleDefinition { Id = "R-ONLY", Because = "Only hosts.", Kind = RuleKind.AllowOnly, From = new TargetSelector { Namespaces = ["Hosts"] }, To = new TargetSelector { Namespaces = ["Web"] } },
            ],
        };

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("App", "net8.0", Compile(source)),
            new AnalyzedProject("App", "net10.0", Compile(source, "MANY")),
        ]);

        report.Violations.Should().HaveCount(2);
        var forbid = report.Violations.First(v => v.Kind == ViolationKind.ForbiddenDependency);
        forbid.ReferenceCount.Should().Be(3);
        forbid.Message.Should().Be("Rule 'R-FORBID' forbids this dependency: Shop.Domain.Order references Web.Client 3 times");
        var only = report.Violations.First(v => v.Kind == ViolationKind.DependencyNotPermitted);
        only.ReferenceCount.Should().Be(3);
        only.Message.Should().Be("Rule 'R-ONLY' allows only selected types to depend on Web.Client: Shop.Domain.Order references Web.Client 3 times");
        report.Violations.Should().OnlyContain(v => v.TargetFrameworks.Count == 0);
    }

    [Fact]
    public void UniqueNamesRule_ReportsNameCollisionWithRuleIdAndBecause()
    {
        var pack = new RulePack
        {
            Name = "test",
            Rules =
            [
                new RuleDefinition
                {
                    Id = "R-UNIQUE",
                    Because = "Names stay unique.",
                    Kind = RuleKind.UniqueNames,
                    From = new TargetSelector { Namespaces = ["Shop.Web.**"] },
                    Against = new TargetSelector { Namespaces = ["Shop.Domain.**"] },
                },
            ],
        };

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("Domain", "net8.0", TestCompilation.Create("namespace Shop.Domain { public class Order { } }")),
            new AnalyzedProject("Domain", "net10.0", TestCompilation.Create("namespace Shop.Domain { public class Order { } }")),
            new AnalyzedProject("Web", null, TestCompilation.Create("namespace Shop.Web { public class Order { } }")),
        ]);

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.NameCollision);
        violation.RuleId.Should().Be("R-UNIQUE");
        violation.Because.Should().Be("Names stay unique.");
        violation.SourceType.Should().Be("Shop.Web.Order");
        violation.Target.Should().Be("Shop.Domain.Order");
    }

    [Fact]
    public void UniqueNamesRule_TypeInTwoTargetFrameworks_ReportsNameCollisionOnce()
    {
        var pack = new RulePack
        {
            Name = "test",
            Rules =
            [
                new RuleDefinition
                {
                    Id = "R-UNIQUE",
                    Because = "Names stay unique.",
                    Kind = RuleKind.UniqueNames,
                    From = new TargetSelector { Namespaces = ["Shop.Web.**"] },
                    Against = new TargetSelector { Namespaces = ["Shop.Domain.**"] },
                },
            ],
        };
        const string web = "namespace Shop.Web { public class Order { } }";

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("Web", "net8.0", TestCompilation.Create(web)),
            new AnalyzedProject("Web", "net10.0", TestCompilation.Create(web)),
            new AnalyzedProject("Domain", null, TestCompilation.Create("namespace Shop.Domain { public class Order { } }")),
        ]);

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.NameCollision);
        violation.RuleId.Should().Be("R-UNIQUE");
        violation.Because.Should().Be("Names stay unique.");
        violation.SourceType.Should().Be("Shop.Web.Order");
        violation.Target.Should().Be("Shop.Domain.Order");
        violation.TargetFrameworks.Should().BeEmpty();
    }

    private static LayerDefinition VLayer(string name, string ns) =>
        new() { Name = name, Id = "V-" + name.ToUpperInvariant(), Because = "Layer " + name + ".", Namespaces = [ns] };

    private static RuleDefinition VRule(string id, TargetSelector from, TargetSelector? to = null) =>
        new() { Id = id, Because = "Because " + id + ".", Kind = RuleKind.Forbid, From = from, To = to ?? new TargetSelector { Namespaces = ["Shop.**"] } };

    private static TargetSelector VNs(string ns) => new() { Namespaces = [ns] };

    private const string ShopWeb = "namespace Shop.Web { public class Page { } }";

    private const string ShopDomain = "namespace Shop.Domain { public class Order { } }";

    [Fact]
    public void Pack_layer_with_no_type_in_the_solution_reports_one_vacuous_layer()
    {
        var pack = new RulePack { Name = "test", Layers = [VLayer("Web", "Shop.Web.**"), VLayer("Ghost", "Nowhere.**")] };

        var report = _validator.Validate(pack, TestCompilation.Create(ShopWeb));

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.VacuousLayer);
        violation.RuleId.Should().Be("V-GHOST");
        violation.SourceType.Should().Be("Ghost");
        violation.Location.Should().BeNull();
    }

    [Fact]
    public void Rule_matching_in_no_project_reports_one_vacuous_rule()
    {
        var pack = new RulePack { Name = "test", Rules = [VRule("R-1", VNs("Nowhere.**"))] };

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("A", null, TestCompilation.Create(ShopWeb)),
            new AnalyzedProject("B", null, TestCompilation.Create(ShopDomain)),
        ]);

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.VacuousRule);
        violation.RuleId.Should().Be("R-1");
        violation.Message.Should().Contain("in the 2 project(s) loaded");
    }

    [Fact]
    public void Rule_matching_in_one_of_two_projects_is_not_vacuous()
    {
        var pack = new RulePack { Name = "test", Rules = [VRule("R-1", VNs("Shop.Web.**"))] };

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("A", null, TestCompilation.Create(ShopWeb)),
            new AnalyzedProject("B", null, TestCompilation.Create(ShopDomain)),
        ]);

        report.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Two_target_frameworks_of_one_project_report_a_vacuous_rule_once()
    {
        var pack = new RulePack { Name = "test", Rules = [VRule("R-1", VNs("Nowhere.**"))] };

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("App", "net8.0", TestCompilation.Create(ShopWeb)),
            new AnalyzedProject("App", "net10.0", TestCompilation.Create(ShopWeb)),
        ]);

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.VacuousRule);
        violation.Message.Should().Contain("in the 1 project(s) loaded", "two target frameworks of one project count once");
    }

    [Fact]
    public void Rule_matching_in_both_target_frameworks_is_not_reported()
    {
        var pack = new RulePack { Name = "test", Rules = [VRule("R-1", VNs("Shop.Web.**"))] };

        var report = _validator.Validate(pack,
        [
            new AnalyzedProject("App", "net8.0", TestCompilation.Create(ShopWeb)),
            new AnalyzedProject("App", "net10.0", TestCompilation.Create(ShopWeb)),
        ]);

        report.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Rule_whose_from_names_one_live_and_one_vacuous_layer_is_not_vacuous()
    {
        var pack = new RulePack
        {
            Name = "test",
            Layers = [VLayer("Web", "Shop.Web.**"), VLayer("Ghost", "Nowhere.**")],
            Rules = [VRule("R-1", new TargetSelector { Layers = ["Web", "Ghost"] })],
        };

        var report = _validator.Validate(pack, TestCompilation.Create(ShopWeb));

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.VacuousLayer);
        violation.RuleId.Should().Be("V-GHOST");
        violation.SourceType.Should().Be("Ghost");
    }

    [Fact]
    public void Child_pack_retargeting_a_vacuous_layer_clears_the_violation()
    {
        var parent = new RulePack { Name = "parent", Layers = [VLayer("Domain", "Nowhere.**")] };
        var child = new RulePack { Name = "child", LayerRetargets = [new LayerRetarget { Name = "Domain", Namespaces = ["Shop.Domain.**"] }] };
        var source = TestCompilation.Create(ShopDomain);

        _validator.Validate(parent, source).Violations.Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.VacuousLayer);

        var merged = new RulePackMerger().Merge([parent, child]);
        merged.IsSuccess.Should().BeTrue();
        _validator.Validate(merged.Value, source).Violations.Should().BeEmpty();
    }

    [Fact]
    public void Vacuity_does_not_depend_on_project_order()
    {
        var pack = new RulePack
        {
            Name = "test",
            Layers = [VLayer("Web", "Shop.Web.**"), VLayer("Ghost", "Nowhere.**")],
            Rules = [VRule("R-1", VNs("Shop.Domain.**"))],
        };
        var a = new AnalyzedProject("A", null, TestCompilation.Create(ShopWeb));
        var b = new AnalyzedProject("B", null, TestCompilation.Create(ShopDomain));

        var forward = _validator.Validate(pack, [a, b]);
        var backward = _validator.Validate(pack, [b, a]);

        forward.Violations.Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.VacuousLayer);
        backward.Violations.Select(v => (v.Kind, v.RuleId, v.Message)).Should().Equal(forward.Violations.Select(v => (v.Kind, v.RuleId, v.Message)));
    }

    [Fact]
    public void Role_matching_no_type_reports_one_vacuous_rule_named_after_the_role_unless_allow_empty()
    {
        RulePack PackWithRole(bool allowEmpty) => new()
        {
            Name = "test",
            Layers = [VLayer("Web", "Shop.Web.**")],
            ComponentRoles = [new ComponentRoleDefinition { Name = "Controller", Layer = "Web", TypeKind = CSharpTypeKind.Class, AllowEmpty = allowEmpty }],
        };

        var report = _validator.Validate(PackWithRole(false), TestCompilation.Create(ShopWeb));

        var violation = report.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.VacuousRule);
        violation.RuleId.Should().Be("Controller");
        violation.Location.Should().BeNull();

        _validator.Validate(PackWithRole(true), TestCompilation.Create(ShopWeb)).Violations.Should().BeEmpty();
    }

    [Fact]
    public void Report_IsValid_is_false_for_a_vacuous_layer()
    {
        var pack = new RulePack { Name = "test", Layers = [VLayer("Ghost", "Nowhere.**")] };

        _validator.Validate(pack, TestCompilation.Create(ShopWeb)).IsValid.Should().BeFalse();
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
