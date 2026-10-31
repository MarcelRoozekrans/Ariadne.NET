using System.Runtime.Versioning;
using Ariadne.NET.Cli;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Tests;

public sealed class ExitCodeTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly string _tempDir;
    private readonly ServiceProvider _provider;

    public ExitCodeTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, ".architecture"));
        File.WriteAllText(
            Path.Combine(_tempDir, ".architecture", "rules.yaml"),
            """
            extends:
              - builtin://clean-architecture
            """);
        Directory.SetCurrentDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddAriadneNETServices();
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

    private void WriteRules(string content) =>
        File.WriteAllText(Path.Combine(_tempDir, ".architecture", "rules.yaml"), content);

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
    public void Init_TemplateWithoutNames_ShouldReturnErrorAndWriteStderr()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);
        try
        {
            Run("init", "--template", " + ").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        error.ToString().Should().Contain("Error: Template expression ' + ' contains no template names.");
    }

    [Fact]
    public void Init_UnknownTemplate_ShouldReturnErrorAndWriteStderr()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);
        try
        {
            Run("init", "--template", "no-such-template").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        error.ToString().Should().Contain("Error: Built-in pack 'no-such-template' not found.");
        error.ToString().Should().Contain("Available packs: ");
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
    public void Rules_MissingRulesFile_TellsUserToRunAriadneInit()
    {
        File.Delete(Path.Combine(_tempDir, ".architecture", "rules.yaml"));
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("rules").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "an expected failure prints the message and the path, not a stack trace");
        lines[0].Should().Be("Error: No .architecture/rules.yaml found. Run 'ariadne init' first.");
        lines[1].Should().Be(".architecture/rules.yaml");
    }

    [Fact]
    public void Validate_RulesExtendUnknownBuiltin_ShouldReturnErrorAndWriteStderr()
    {
        WriteRules("""
            extends:
              - builtin://nope
            """);
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("validate", "src").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Error: Built-in pack 'nope' not found.");
        lines[1].Should().StartWith("Available packs: ");
    }

    [Fact]
    public void Validate_MalformedRules_ShouldReturnErrorWithLine()
    {
        WriteRules("name: x\nlayers: [unclosed");
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("validate", "src").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        error.ToString().Should().StartWith("Error: Rule pack file '.architecture/rules.yaml' could not be parsed: ")
            .And.Contain("line");
        error.ToString().Should().NotContain(" at ", "an expected failure prints no stack trace");
    }

    [UnixNonRootFact]
    [UnsupportedOSPlatform("windows")]
    public void Validate_UnreadableRules_ShouldReturnErrorAndWriteStderr()
    {
        var rulesPath = Path.Combine(_tempDir, ".architecture", "rules.yaml");
        File.SetUnixFileMode(rulesPath, UnixFileMode.None);
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("validate", "src").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
            File.SetUnixFileMode(rulesPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "an expected failure prints the message and the OS reason, not a stack trace");
        lines[0].Should().Be("Error: Rule pack file '.architecture/rules.yaml' could not be read.");
    }

    [Fact]
    public void Sync_MissingImportsFile_ShouldReturnError()
    {
        Run("sync").Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void UnexpectedException_ReportsMessageThenStackTraceAndReturnsError()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        int exitCode;
        try
        {
            exitCode = CliRootCommand.Invoke(new ThrowingServiceProvider(), ["rules"]);
        }
        finally
        {
            Console.SetError(originalError);
        }

        exitCode.Should().Be(ExitCodes.Error);
        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Error: boom");
        error.ToString().Should().Contain("System.InvalidOperationException: boom").And.Contain(" at ");
    }

    private sealed class ThrowingServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new InvalidOperationException("boom");
    }
}
