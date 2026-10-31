using Ariadne.NET.Abstractions;
using Ariadne.NET.Validation;
using FluentAssertions;

namespace Ariadne.NET.Tests.Validation;

public sealed class LinterTests : IDisposable
{
    private readonly string _tempDir;

    public LinterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-lint-{Guid.NewGuid():N}");
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
            ],
            NamingConventions = new NamingConventions
            {
                Namespace = "{SolutionName}.{Feature}.{Layer}",
                FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
            },
        };
    }

    [Fact]
    public void Lint_CorrectStructure_ShouldBeClean()
    {
        var dir = Path.Combine(_tempDir, "MyApp.Orders.Domain");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "Order.cs"),
            """
            namespace MyApp.Orders.Domain;
            public class Order { }
            """);

        var linter = new Linter();
        var result = linter.Lint(CreateTestPack(), _tempDir);

        result.IsClean.Should().BeTrue();
        result.FilesScanned.Should().Be(1);
    }

    [Fact]
    public void Lint_NamespaceMismatchesDirectory_ShouldReportViolation()
    {
        var dir = Path.Combine(_tempDir, "MyApp.Orders.Domain");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "Handler.cs"),
            """
            namespace MyApp.Orders.Application;
            public class Handler { }
            """);

        var linter = new Linter();
        var result = linter.Lint(CreateTestPack(), _tempDir);

        result.IsClean.Should().BeFalse();
        result.Violations.Should().ContainSingle();
        result.Violations[0].Category.Should().Be("NamespaceMismatch");
    }

    [Fact]
    public void Lint_EmptyDirectory_ShouldBeClean()
    {
        var linter = new Linter();
        var result = linter.Lint(CreateTestPack(), _tempDir);

        result.IsClean.Should().BeTrue();
        result.FilesScanned.Should().Be(0);
    }
}
