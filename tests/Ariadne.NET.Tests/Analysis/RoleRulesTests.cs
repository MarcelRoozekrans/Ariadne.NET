using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Tests.Analysis;

public class RoleRulesTests
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
            new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class },
            new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record },
            new ComponentRoleDefinition { Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
            new ComponentRoleDefinition { Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface, AlternativeNames = ["Repository"] },
        ],
    };

    private static (string Path, string Source) InterfaceFile =>
        ("Iface.cs", "namespace MyApp.Orders.Domain { public interface IOrderRepository { } }");

    [Fact]
    public void CommandIsClass_TypeKindMismatch()
    {
        var v = Check("namespace MyApp.Orders.Application; public class CreateOrderCommand { }").Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.TypeKindMismatch);
        v.RuleId.Should().Be("ROLE-Command");
        v.SourceType.Should().Be("MyApp.Orders.Application.CreateOrderCommand");
        v.Location.Should().Be(new SourceLocation("Test.cs", 1, 50));
        v.Message.Should().Be("Type 'CreateOrderCommand' matches role 'Command' and should be a 'Record' but is declared as a 'Class'");
    }

    [Fact]
    public void TypeCoveredByTwoLayers_MatchesNoRole() =>
        Check("namespace MyApp.Application.Domain; public class CreateOrderCommand { }").Should().BeEmpty();

    [Fact]
    public void CommandIsRecord_NoViolation() =>
        Check("namespace MyApp.Orders.Application; public record CreateOrderCommand;").Should().BeEmpty();

    [Fact]
    public void RepositoryIsPublic_AccessibilityMismatch()
    {
        var v = Check("namespace MyApp.Orders.Infrastructure; public class OrderRepository { }", InterfaceFile)
            .Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.AccessibilityMismatch);
        v.RuleId.Should().Be("CODE-002");
        v.Location.Should().Be(new SourceLocation("Test.cs", 1, 53));
        v.Message.Should().Be("Type 'OrderRepository' matches role 'Repository' and should be 'internal' but is declared 'public'");
    }

    [Fact]
    public void RepositoryIsInternal_NoViolation() =>
        Check("namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }", InterfaceFile).Should().BeEmpty();

    [Fact]
    public void RepositoryWithoutInterface_MissingInterface()
    {
        var v = Check("namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }")
            .Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.MissingInterface);
        v.RuleId.Should().Be("CODE-001");
        v.SourceType.Should().Be("MyApp.Orders.Infrastructure.OrderRepository");
        v.Location.Should().BeNull();
        v.Message.Should().Be("Type 'OrderRepository' matches role 'Repository' which requires interface role 'RepositoryInterface', but no matching interface was found");
    }

    [Fact]
    public void RepositoryWithInterface_NoMissingInterface() =>
        Check(
            ("Infra.cs", "namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }"),
            ("Domain.cs", "namespace MyApp.Orders.Domain; public interface IOrderRepository { }"))
            .Should().BeEmpty();

    [Fact]
    public void MissingInterface_InterfaceInAnotherFile_NoViolation() =>
        Check(
            ("A.cs", "namespace MyApp.Orders.Infrastructure { internal class OrderRepository { } }"),
            ("B.cs", "namespace MyApp.Orders.Domain { public interface IOrderRepository { } }"))
            .Should().BeEmpty();

    [Fact]
    public void RecordStruct_IsRecordStructKind() =>
        Check("namespace MyApp.Orders.Domain; public record struct OrderEntity;").Should().ContainSingle()
            .Which.Message.Should().EndWith("should be a 'Class' but is declared as a 'RecordStruct'");

    [Fact]
    public void Record_IsRecordKind() =>
        Check("namespace MyApp.Orders.Domain; public record OrderEntity;").Should().ContainSingle()
            .Which.Message.Should().EndWith("should be a 'Class' but is declared as a 'Record'");

    [Fact]
    public void Struct_IsStructKind() =>
        Check("namespace MyApp.Orders.Domain; public struct OrderEntity;").Should().ContainSingle()
            .Which.Message.Should().EndWith("should be a 'Class' but is declared as a 'Struct'");

    [Fact]
    public void TypeWithoutAccessModifier_CountsAsInternal() =>
        Check("namespace MyApp.Orders.Infrastructure; class OrderRepository { }", InterfaceFile).Should().BeEmpty();

    [Fact]
    public void ProtectedInternal_IsNotInternal() =>
        Check(
            "namespace MyApp.Orders.Infrastructure { public class Outer { protected internal class OrderRepository { } } }",
            InterfaceFile)
            .Should().ContainSingle().Which.Message.Should().EndWith("but is declared 'protected internal'");

    [Fact]
    public void NestedTypeMatchingRole_IsChecked() =>
        Check("namespace MyApp.Orders.Application { public class Outer { public class SaveCommand { } } }")
            .Should().ContainSingle().Which.SourceType.Should().Be("MyApp.Orders.Application.Outer.SaveCommand");

    [Fact]
    public void GeneratedTypeMatchingRole_NotChecked() =>
        Check(("Save.g.cs", "namespace MyApp.Orders.Application { public class SaveCommand { } }")).Should().BeEmpty();

    [Fact]
    public void FirstRoleInPackOrderWins() =>
        Check("namespace MyApp.Orders.Domain; public class OrderRepository { }").Should().ContainSingle()
            .Which.RuleId.Should().Be("ROLE-RepositoryInterface");

    [Fact]
    public void NameMatchIsCaseInsensitive() =>
        Check("namespace MyApp.Orders.Application; public class SaveCOMMAND { }").Should().ContainSingle()
            .Which.RuleId.Should().Be("ROLE-Command");

    [Fact]
    public void GenericType_MatchesBySimpleName() =>
        Check("namespace MyApp.Orders.Application; public class SaveCommand<T> { }").Should().ContainSingle()
            .Which.Message.Should().StartWith("Type 'SaveCommand' matches");

    [Fact]
    public void Violations_AreOrderedByLocationThenRuleId_UnlocatedLast()
    {
        var violations = Check(
            ("B.cs", "namespace MyApp.Orders.Application { public class ZCommand { } }"),
            ("A.cs", "namespace MyApp.Orders.Infrastructure { public class BRepository { } public class ARepository { } }"));

        violations.Select(v => (v.Kind, v.Location?.FilePath, v.SourceType)).Should().Equal(
            (ViolationKind.AccessibilityMismatch, "A.cs", "MyApp.Orders.Infrastructure.BRepository"),
            (ViolationKind.AccessibilityMismatch, "A.cs", "MyApp.Orders.Infrastructure.ARepository"),
            (ViolationKind.TypeKindMismatch, "B.cs", "MyApp.Orders.Application.ZCommand"),
            (ViolationKind.MissingInterface, null, "MyApp.Orders.Infrastructure.ARepository"),
            (ViolationKind.MissingInterface, null, "MyApp.Orders.Infrastructure.BRepository"));
    }

    [Fact]
    public void EnumAndDelegateNamedLikeRoles_AreNotMatched() =>
        Check("""
            namespace MyApp.Orders.Infrastructure
            {
                public enum OrderRepository { A }
                public delegate void SaveRepository();
            }
            """).Should().BeEmpty();

    [Fact]
    public void EnumNamedLikeInterfaceRole_DoesNotSatisfyMissingInterface() =>
        Check(
            ("A.cs", "namespace MyApp.Orders.Infrastructure { internal class OrderRepository { } }"),
            ("B.cs", "namespace MyApp.Orders.Domain { public enum StateRepository { A } public delegate void RunRepository(); }"))
            .Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.MissingInterface);

    [Fact]
    public void NestedTypeWithoutModifier_IsPrivate() =>
        Check(
            "namespace MyApp.Orders.Infrastructure { public class Outer { class OrderRepository { } } }",
            InterfaceFile)
            .Should().ContainSingle().Which.Message.Should().EndWith("but is declared 'private'");

    [Fact]
    public void PartialTypeWithGeneratedFirstDeclaration_IsLocatedAtHandWrittenOne()
    {
        var violations = Check(
            ("A.g.cs", "namespace MyApp.Orders.Application { public partial class SaveCommand { } }"),
            ("B.cs", "namespace MyApp.Orders.Application { public partial class SaveCommand { } }"));

        violations.Should().ContainSingle().Which.Location.Should().Be(new SourceLocation("B.cs", 1, 59));
    }

    [Fact]
    public void TypeInNamespaceWithoutLayer_IsNotChecked() =>
        Check("namespace MyApp.Orders.Misc; public class SaveCommand { }").Should().BeEmpty();

    [Fact]
    public void RoleWithEmptyDescription_UsesTheFallbackBecause() =>
        Check("namespace MyApp.Orders.Application; public class CreateOrderCommand { }").Should().ContainSingle()
            .Which.Because.Should().Be("Role 'Command' defines this shape.");

    [Fact]
    public void RoleWithDescription_UsesItAsBecause()
    {
        var pack = new RulePack
        {
            Name = "test",
            Layers = Pack.Layers,
            ComponentRoles =
            [
                new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record, Description = "Commands are immutable." },
            ],
        };

        Check(pack, [("Test.cs", "namespace MyApp.Orders.Application; public class CreateOrderCommand { }")])
            .Should().ContainSingle().Which.Because.Should().Be("Commands are immutable.");
    }

    [Fact]
    public void AccessibilityViolation_ReportsCode002()
    {
        var v = Check("namespace MyApp.Orders.Infrastructure; public class OrderRepository { }", InterfaceFile)
            .Should().ContainSingle().Subject;
        v.RuleId.Should().Be("CODE-002");
        v.Because.Should().Be("A role that asks for internal accessibility keeps its implementation out of the public API.");
    }

    [Fact]
    public void MissingInterface_ReportsCode001WithItsBecause()
    {
        var v = Check("namespace MyApp.Orders.Infrastructure; internal class OrderRepository { }")
            .Should().ContainSingle().Subject;
        v.RuleId.Should().Be("CODE-001");
        v.Because.Should().Be("A role that requires an interface needs a type of its interface role in the solution.");
    }

    [Fact]
    public void OverlappingRoles_StillGiveOneViolationPerType()
    {
        var pack = new RulePack
        {
            Name = "test",
            Layers = Pack.Layers,
            ComponentRoles =
            [
                new ComponentRoleDefinition { Name = "Service", Layer = "Application", TypeKind = CSharpTypeKind.Interface },
                new ComponentRoleDefinition { Name = "ApplicationService", Layer = "Application", TypeKind = CSharpTypeKind.Interface },
            ],
        };

        Check(pack, [("Test.cs", "namespace MyApp.Orders.Application; public class OrderApplicationService { }")])
            .Should().ContainSingle().Which.RuleId.Should().Be("ROLE-Service");
    }

    private static IReadOnlyList<ArchitectureViolation> Check(string source, params (string Path, string Source)[] more) =>
        Check([("Test.cs", source), .. more]);

    private static IReadOnlyList<ArchitectureViolation> Check(params (string Path, string Source)[] files) =>
        Check(Pack, files);

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, (string Path, string Source)[] files)
    {
        var compilation = TestCompilation.Create(files);
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        return RoleRules.Check(graph, new LayerMap(pack), pack);
    }
}
