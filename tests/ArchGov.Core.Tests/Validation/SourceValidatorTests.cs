using ArchGov.Core.RuleSchema;
using ArchGov.Core.Validation;
using FluentAssertions;

namespace ArchGov.Core.Tests.Validation;

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
}
