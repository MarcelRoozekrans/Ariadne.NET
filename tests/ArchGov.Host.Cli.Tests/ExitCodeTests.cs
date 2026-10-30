using ArchGov.Host.Cli;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ArchGov.Host.Cli.Tests;

public sealed class ExitCodeTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly string _tempDir;
    private readonly ServiceProvider _provider;

    public ExitCodeTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), $"archgov-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, ".architecture"));
        File.WriteAllText(
            Path.Combine(_tempDir, ".architecture", "rules.yaml"),
            """
            extends:
              - builtin://clean-architecture
            """);
        Directory.SetCurrentDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddArchGovCoreServices();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        _provider.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private int Run(params string[] args) => CliRootCommand.Invoke(_provider, args);

    private void WriteSource(string project, string fileName, string content)
    {
        var dir = Path.Combine(_tempDir, "src", project);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    [Fact]
    public void Validate_CleanSource_ShouldReturnSuccess()
    {
        WriteSource("MyApp.Orders.Domain", "Order.cs", """
            namespace MyApp.Orders.Domain;
            public class Order { }
            """);

        Run("validate", "src").Should().Be(ExitCodes.Success);
    }

    [Fact]
    public void Validate_DependencyViolation_ShouldReturnViolations()
    {
        WriteSource("MyApp.Orders.Domain", "Order.cs", """
            using MyApp.Orders.Infrastructure;
            namespace MyApp.Orders.Domain;
            public class Order { }
            """);

        Run("validate", "src").Should().Be(ExitCodes.Violations);
    }

    [Fact]
    public void Validate_MissingDirectory_ShouldReturnError()
    {
        Run("validate", "does-not-exist").Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void Lint_NamespaceMismatch_ShouldReturnViolations()
    {
        WriteSource("MyApp.Orders.Domain", "Order.cs", """
            namespace MyApp.Orders.Application;
            public class Order { }
            """);

        Run("lint", "src").Should().Be(ExitCodes.Violations);
    }

    [Fact]
    public void Lint_MissingDirectory_ShouldReturnError()
    {
        Run("lint", "does-not-exist").Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void Generate_UnknownRole_ShouldReturnError()
    {
        Run("generate", "--role", "NoSuchRole", "--name", "Order", "--solution", "MyApp", "--feature", "Orders")
            .Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void Generate_KnownRole_ShouldReturnSuccess()
    {
        Run("generate", "--role", "Entity", "--name", "Order", "--solution", "MyApp", "--feature", "Orders")
            .Should().Be(ExitCodes.Success);
    }

    [Fact]
    public void MissingRulesFile_ShouldReturnError()
    {
        File.Delete(Path.Combine(_tempDir, ".architecture", "rules.yaml"));

        Run("rules").Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void Sync_MissingImportsFile_ShouldReturnError()
    {
        Run("sync").Should().Be(ExitCodes.Error);
    }
}
