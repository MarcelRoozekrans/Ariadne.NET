using FluentAssertions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Validation;
using Ariadne.NET.Mcp.Tools;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class ValidationToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly SourceValidator _validator;
    private readonly Linter _linter;

    public ValidationToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
        _validator = new SourceValidator();
        _linter = new Linter();
    }

    [Fact]
    public void ValidateSource_EmptyDir_ShouldReturnValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = ValidationTools.ValidateSource(
                _helper, _validator,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsValid.Should().BeTrue();
            result.FilesScanned.Should().Be(0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LintSource_EmptyDir_ShouldReturnClean()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = ValidationTools.LintSource(
                _helper, _linter,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsClean.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateSource_DependencyViolation_ShouldReturnInvalid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var domainDir = Path.Combine(tempDir, "MyApp.Orders.Domain");
            Directory.CreateDirectory(domainDir);
            File.WriteAllText(Path.Combine(domainDir, "Order.cs"), """
                using MyApp.Orders.Infrastructure;

                namespace MyApp.Orders.Domain;

                public class Order { }
                """);

            var infraDir = Path.Combine(tempDir, "MyApp.Orders.Infrastructure");
            Directory.CreateDirectory(infraDir);
            File.WriteAllText(Path.Combine(infraDir, "Db.cs"), """
                namespace MyApp.Orders.Infrastructure;

                public class Db { }
                """);

            var result = ValidationTools.ValidateSource(
                _helper, _validator,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsValid.Should().BeFalse();
            result.Violations.Should().ContainSingle(v =>
                v.ViolationType == "DependencyViolation"
                && v.Message.Contains("Domain")
                && v.Message.Contains("Infrastructure"));
            result.FilesScanned.Should().Be(2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateSource_CorrectStructure_ShouldReturnValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var domainDir = Path.Combine(tempDir, "MyApp.Orders.Domain");
            Directory.CreateDirectory(domainDir);
            File.WriteAllText(Path.Combine(domainDir, "Order.cs"), """
                namespace MyApp.Orders.Domain;

                public class Order
                {
                    public Guid Id { get; init; }
                }
                """);

            var appDir = Path.Combine(tempDir, "MyApp.Orders.Application");
            Directory.CreateDirectory(appDir);
            File.WriteAllText(Path.Combine(appDir, "OrderService.cs"), """
                using MyApp.Orders.Domain;

                namespace MyApp.Orders.Application;

                public class OrderService { }
                """);

            var result = ValidationTools.ValidateSource(
                _helper, _validator,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsValid.Should().BeTrue();
            result.FilesScanned.Should().Be(2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LintSource_NamespaceMismatch_ShouldReturnViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var domainDir = Path.Combine(tempDir, "MyApp.Orders.Domain");
            Directory.CreateDirectory(domainDir);
            File.WriteAllText(Path.Combine(domainDir, "Misplaced.cs"), """
                namespace MyApp.Orders.Infrastructure;

                public class Misplaced { }
                """);

            var result = ValidationTools.LintSource(
                _helper, _linter,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsClean.Should().BeFalse();
            result.Violations.Should().ContainSingle(v =>
                v.Category == "NamespaceMismatch");
            result.FilesScanned.Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateSource_WithHexagonalTemplate_ShouldUseCorrectRules()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var coreDir = Path.Combine(tempDir, "MyApp.Orders.Core");
            Directory.CreateDirectory(coreDir);
            File.WriteAllText(Path.Combine(coreDir, "Order.cs"), """
                namespace MyApp.Orders.Core;

                public class Order { }
                """);

            var result = ValidationTools.ValidateSource(
                _helper, _validator,
                sourceDirectory: tempDir,
                template: "hexagonal-architecture");

            result.IsValid.Should().BeTrue();
            result.FilesScanned.Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
