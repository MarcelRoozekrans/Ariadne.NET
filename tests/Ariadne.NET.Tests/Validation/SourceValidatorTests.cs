using Ariadne.NET.RuleSchema;
using Ariadne.NET.Validation;
using FluentAssertions;

namespace Ariadne.NET.Tests.Validation;

public sealed class SourceValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public SourceValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"archgov-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static RulePack CreateTestPack()
    {
        return new RulePack
        {
            Name = "test",
            Layers =
            [
                new LayerDefinition { Name = "Domain", AllowedDependencies = [] },
                new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
                new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
            ],
            NamingConventions = new NamingConventions
            {
                Namespace = "{SolutionName}.{Feature}.{Layer}",
                FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
            },
        };
    }

    [Fact]
    public void Validate_ValidFile_ShouldReturnNoViolations()
    {
        var domainDir = Path.Combine(_tempDir, "MyApp.Orders.Domain");
        Directory.CreateDirectory(domainDir);
        File.WriteAllText(
            Path.Combine(domainDir, "Order.cs"),
            """
            namespace MyApp.Orders.Domain;

            public class Order { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPack(), _tempDir);

        result.FilesScanned.Should().Be(1);
        result.Violations.Should().BeEmpty();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DomainDependsOnInfrastructure_ShouldReturnViolation()
    {
        var domainDir = Path.Combine(_tempDir, "MyApp.Orders.Domain");
        Directory.CreateDirectory(domainDir);
        File.WriteAllText(
            Path.Combine(domainDir, "Order.cs"),
            """
            using MyApp.Orders.Infrastructure;

            namespace MyApp.Orders.Domain;

            public class Order { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPack(), _tempDir);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle();
        result.Violations[0].ViolationType.Should().Be("DependencyViolation");
    }

    [Fact]
    public void Validate_ApplicationDependsOnDomain_ShouldBeValid()
    {
        var appDir = Path.Combine(_tempDir, "MyApp.Orders.Application");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(
            Path.Combine(appDir, "CreateOrderHandler.cs"),
            """
            using MyApp.Orders.Domain;

            namespace MyApp.Orders.Application;

            public class CreateOrderHandler { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPack(), _tempDir);

        result.IsValid.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyDirectory_ShouldReturnValidWithZeroFiles()
    {
        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPack(), _tempDir);

        result.IsValid.Should().BeTrue();
        result.FilesScanned.Should().Be(0);
    }

    private static RulePack CreateTestPackWithRoles()
    {
        return new RulePack
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
                new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class },
                new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record },
                new ComponentRoleDefinition { Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
                new ComponentRoleDefinition { Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface, AlternativeNames = ["Repository"] },
            ],
            NamingConventions = new NamingConventions
            {
                Namespace = "{SolutionName}.{Feature}.{Layer}",
                FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
            },
        };
    }

    [Fact]
    public void Validate_CommandIsClass_ShouldReturnTypeKindMismatch()
    {
        var appDir = Path.Combine(_tempDir, "MyApp.Orders.Application");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(
            Path.Combine(appDir, "CreateOrderCommand.cs"),
            """
            namespace MyApp.Orders.Application;

            public class CreateOrderCommand { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.ViolationType == "TypeKindMismatch");
    }

    [Fact]
    public void Validate_CommandIsRecord_ShouldBeValid()
    {
        var appDir = Path.Combine(_tempDir, "MyApp.Orders.Application");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(
            Path.Combine(appDir, "CreateOrderCommand.cs"),
            """
            namespace MyApp.Orders.Application;

            public record CreateOrderCommand;
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

        result.Violations.Where(v => v.ViolationType == "TypeKindMismatch").Should().BeEmpty();
    }

    [Fact]
    public void Validate_RepositoryIsPublic_ShouldReturnAccessibilityMismatch()
    {
        var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
        Directory.CreateDirectory(infraDir);
        File.WriteAllText(
            Path.Combine(infraDir, "OrderRepository.cs"),
            """
            namespace MyApp.Orders.Infrastructure;

            public class OrderRepository { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

        result.Violations.Should().Contain(v => v.ViolationType == "AccessibilityMismatch");
    }

    [Fact]
    public void Validate_RepositoryIsInternal_ShouldNotReportAccessibilityMismatch()
    {
        var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
        Directory.CreateDirectory(infraDir);
        File.WriteAllText(
            Path.Combine(infraDir, "OrderRepository.cs"),
            """
            namespace MyApp.Orders.Infrastructure;

            internal class OrderRepository { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

        result.Violations.Where(v => v.ViolationType == "AccessibilityMismatch").Should().BeEmpty();
    }

    [Fact]
    public void Validate_RepositoryWithoutInterface_ShouldReturnMissingInterface()
    {
        // Repository exists but no matching RepositoryInterface
        var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
        Directory.CreateDirectory(infraDir);
        File.WriteAllText(
            Path.Combine(infraDir, "OrderRepository.cs"),
            """
            namespace MyApp.Orders.Infrastructure;

            internal class OrderRepository { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

        result.Violations.Should().Contain(v => v.ViolationType == "MissingInterface");
    }

    [Fact]
    public void Validate_RepositoryWithInterface_ShouldNotReportMissingInterface()
    {
        // Both Repository and RepositoryInterface exist
        var infraDir = Path.Combine(_tempDir, "MyApp.Orders.Infrastructure");
        Directory.CreateDirectory(infraDir);
        File.WriteAllText(
            Path.Combine(infraDir, "OrderRepository.cs"),
            """
            namespace MyApp.Orders.Infrastructure;

            internal class OrderRepository { }
            """);

        var domainDir = Path.Combine(_tempDir, "MyApp.Orders.Domain");
        Directory.CreateDirectory(domainDir);
        File.WriteAllText(
            Path.Combine(domainDir, "IOrderRepository.cs"),
            """
            namespace MyApp.Orders.Domain;

            public interface IOrderRepository { }
            """);

        var validator = new SourceValidator();
        var result = validator.Validate(CreateTestPackWithRoles(), _tempDir);

        result.Violations.Where(v => v.ViolationType == "MissingInterface").Should().BeEmpty();
    }
}
