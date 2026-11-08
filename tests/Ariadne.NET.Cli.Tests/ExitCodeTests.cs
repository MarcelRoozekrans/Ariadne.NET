using System.Runtime.Versioning;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Cli;
using Ariadne.NET.Cli.Commands;
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
        FixtureProjects.EnsureRestored();
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, ".architecture"));
        File.WriteAllText(
            Path.Combine(_tempDir, ".architecture", "rules.yaml"),
            """
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            """);
        Directory.SetCurrentDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
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

    private int Run(params string[] args) => CliRootCommand.InvokeAsync(_provider, args).GetAwaiter().GetResult();

    // A layer that covers no type fails the run, so a test on a small fixture writes a pack of the layers it has.
    private void WriteLayerRules(params string[] layers)
    {
        var lines = new List<string> { "schema: 2", "name: shop", "layers:" };
        foreach (var layer in layers)
        {
            lines.Add($"  - name: {layer}");
            lines.Add($"    id: T-{layer.ToUpperInvariant()}");
            lines.Add($"    because: The {layer} layer.");
            lines.Add($"    namespaces: [\"**.{layer}.**\"]");
            lines.Add(string.Equals(layer, "Domain", StringComparison.Ordinal) ? "    allowedDependencies: []" : "    allowedDependencies: [Domain]");
        }

        WriteRules(string.Join('\n', lines));
    }

    private void WriteRules(string content) =>
        File.WriteAllText(Path.Combine(_tempDir, ".architecture", "rules.yaml"), content);

    private void WriteSource(string project, string fileName, string content)
    {
        var dir = Path.Combine(_tempDir, "src", project);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    [Fact]
    public void Validate_CleanFixture_ShouldReturnSuccess()
    {
        var (exitCode, stdout) = RunCapturingStdout("validate", FixtureProjects.Path("fourlayer", "Shop.csproj"));

        exitCode.Should().Be(ExitCodes.Success);
        stdout.Should().Contain("Result: PASS - No violations found.");
    }

    [Fact]
    public void Validate_BrokenFixture_ShouldReturnViolations()
    {
        WriteLayerRules("Domain", "Infrastructure");
        var (exitCode, stdout) = RunCapturingStdout("validate", FixtureProjects.Path("broken", "Shop.slnx"));

        exitCode.Should().Be(ExitCodes.Violations);
        stdout.Should().Contain("Result: FAIL - 1 violation(s) found.")
            .And.Contain("[DependencyNotAllowed] Shop.Domain")
            .And.Contain("Order.cs:");
    }

    [Fact]
    public void Validate_BrokenFixture_ShowsViolationFileRelativeToTheSolutionDirectory()
    {
        var (exitCode, stdout) = RunCapturingStdout("validate", FixtureProjects.Path("broken", "Shop.slnx"));

        exitCode.Should().Be(ExitCodes.Violations);
        stdout.Should().Contain("  [DependencyNotAllowed] Shop.Domain/Order.cs:").And.NotContain(FixtureProjects.Root);
    }

    [Fact]
    public void Validate_ProjectFolder_ShowsViolationFilesRelativeToTheFolder()
    {
        var (exitCode, stdout) = RunCapturingStdout("validate", FixtureProjects.Path("multitarget"));

        exitCode.Should().Be(ExitCodes.Violations);
        stdout.Should().Contain("  [DependencyNotAllowed] Domain/Order.cs:").And.NotContain(FixtureProjects.Root);
    }

    [Fact]
    public void Validate_ViolationInSomeTargetFrameworks_ListsThemAfterTheLocation()
    {
        var (exitCode, stdout) = RunCapturingStdout("validate", FixtureProjects.Path("multitarget", "Multi.csproj"));

        exitCode.Should().Be(ExitCodes.Violations);
        stdout.Should().MatchRegex(@"(?m)^  \[DependencyNotAllowed\] Domain/Order\.cs:\d+ \(net10\.0\)\r?$");
    }

    [Fact]
    public void Print_SkippedProjectAndViolationWithoutLocation_WritesBothLines()
    {
        var report = new ArchitectureReport
        {
            Violations =
            [
                new ArchitectureViolation
                {
                    RuleId = "r",
                    Kind = ViolationKind.TypeKindMismatch,
                    SourceType = "Shop.Domain.Order",
                    Message = "Order must be a record",
                },
            ],
            Projects = [new CheckedProject("Shop.Domain", ["net10.0"])],
            SkippedProjects = [new SkippedProject("Shop.Docs", "no compilation")],
            TypesChecked = 4,
        };
        using var writer = new StringWriter();

        var exitCode = ValidateCommand.Print(writer, report, _tempDir);

        exitCode.Should().Be(ExitCodes.Violations);
        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Equal(
            "Checked 4 types in 1 projects",
            "Skipped Shop.Docs: no compilation",
            "Result: FAIL - 1 violation(s) found.",
            "  [TypeKindMismatch] Shop.Domain.Order",
            "    Order must be a record");
    }

    [Fact]
    public void Validate_reports_a_vacuous_layer_by_kind_name()
    {
        var (exitCode, stdout) = RunCapturingStdout("validate", FixtureProjects.Path("broken", "Shop.slnx"));

        exitCode.Should().Be(ExitCodes.Violations);
        stdout.Should().Contain("  [VacuousLayer] Application")
            .And.Contain("Layer 'Application' covers no type");
    }

    [Fact]
    public void Print_vacuous_violations_without_a_location_show_the_kind_and_the_name()
    {
        var report = new ArchitectureReport
        {
            Violations =
            [
                new ArchitectureViolation { RuleId = "L", Kind = ViolationKind.VacuousLayer, SourceType = "Domain", Message = "Layer 'Domain' covers no type in the 1 project(s) loaded." },
                new ArchitectureViolation { RuleId = "SH-1", Kind = ViolationKind.VacuousRule, SourceType = "SH-1", Message = "Rule 'SH-1' selects no type." },
            ],
            Projects = [new CheckedProject("Shop.Domain", ["net10.0"])],
            TypesChecked = 1,
        };
        using var writer = new StringWriter();

        ValidateCommand.Print(writer, report, _tempDir);

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain("  [VacuousLayer] Domain").And.Contain("  [VacuousRule] SH-1");
    }

    [Fact]
    public void Describer_shows_allowEmpty_only_when_true()
    {
        WriteRules("""
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            rules:
              - id: SH-030
                because: Reserved for later.
                allowEmpty: true
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["Later.**"] }
              - id: SH-031
                because: Active now.
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["Now.**"] }
            """);

        var (_, rules) = RunCapturingStdout("rules");
        var ruleLines = rules.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .SkipWhile(l => !string.Equals(l, "Rules:", StringComparison.Ordinal)).Skip(1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)).ToArray();
        ruleLines.Should().Equal(
            "  SH-030 forbid layers Domain -> namespaces Later.**",
            "    allowEmpty",
            "    Reserved for later.",
            "  SH-031 forbid layers Domain -> namespaces Now.**",
            "    Active now.");
    }

    [Fact]
    public void Roles_marks_allowEmpty_only_when_true()
    {
        WriteRules("""
            schema: 2
            name: shop
            layers:
              - { name: Domain, id: SH-L-D, because: Domain., namespaces: ["**.Domain.**"] }
            componentRoles:
              - { name: Reserved, layer: Domain, description: Later., allowEmpty: true }
              - { name: Active, layer: Domain, description: Now. }
            """);

        var (exitCode, stdout) = RunCapturingStdout("roles");

        exitCode.Should().Be(ExitCodes.Success);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle(l => l.Contains("Reserved", StringComparison.Ordinal)).Which.Should().EndWith("Later. (allowEmpty)");
        lines.Should().ContainSingle(l => l.Contains("Active", StringComparison.Ordinal)).Which.Should().EndWith("Now.");
    }

    [Fact]
    public void Print_ViolationInSeveralTargetFrameworks_ListsThemCommaSeparated()
    {
        var report = new ArchitectureReport
        {
            Violations =
            [
                new ArchitectureViolation
                {
                    RuleId = "r",
                    Kind = ViolationKind.TypeKindMismatch,
                    SourceType = "Shop.Domain.Order",
                    Message = "Order must be a record",
                    TargetFrameworks = ["net8.0", "net9.0"],
                },
            ],
            Projects = [new CheckedProject("Shop.Domain", ["net10.0", "net8.0", "net9.0"])],
            TypesChecked = 1,
        };
        using var writer = new StringWriter();

        ValidateCommand.Print(writer, report, _tempDir);

        writer.ToString().Split(Environment.NewLine).Should().Contain("  [TypeKindMismatch] Shop.Domain.Order (net8.0, net9.0)");
    }

    [Fact]
    public void Validate_MissingPath_ShouldReturnError()
    {
        Run("validate", "does-not-exist").Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void Validate_DirectoryWithTwoSolutions_ShouldReturnError()
    {
        var (exitCode, stderr) = RunCapturingStderr("validate", FixtureProjects.Path("clean"));

        exitCode.Should().Be(ExitCodes.Error);
        stderr.Should().StartWith("Error: ");
    }

    [Fact]
    public void Validate_CompileErrorFixture_ShouldReturnError()
    {
        Run("validate", FixtureProjects.Path("compile-error", "Broken.csproj")).Should().Be(ExitCodes.Error);
    }

    [Fact]
    public void Init_WritesASchema2PackThatValidates()
    {
        Run("init", "--template", "clean-architecture+cqrs").Should().Be(ExitCodes.Success);

        var written = File.ReadAllText(Path.Combine(_tempDir, ".architecture", "rules.yaml"));
        written.Should().StartWith("schema: 2");
        written.Should().Contain("- builtin://clean-architecture").And.Contain("- builtin://cqrs");

        // The fourlayer fixture has a type in each of the four layers of clean-architecture.
        Run("validate", FixtureProjects.Path("fourlayer", "Shop.csproj")).Should().Be(ExitCodes.Success);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("~")]
    [InlineData("true")]
    [InlineData("2")]
    [InlineData("#tag")]
    public void Init_InADirectoryWhoseNameIsNotAPlainYamlString_WritesALoadablePack(string directoryName) =>
        AssertInitWritesALoadablePackIn(directoryName);

    // A colon is not valid in a Windows directory name, so this row only runs on Unix.
    [UnixFact]
    public void Init_InADirectoryWhoseNameContainsAColon_WritesALoadablePack() =>
        AssertInitWritesALoadablePackIn("a: b");

    private void AssertInitWritesALoadablePackIn(string directoryName)
    {
        var directory = Path.Combine(_tempDir, directoryName);
        Directory.CreateDirectory(directory);
        Directory.SetCurrentDirectory(directory);

        Run("init").Should().Be(ExitCodes.Success);

        var loaded = new Ariadne.NET.RuleLoader.YamlRulePackLoader().LoadFromFile(Path.Combine(directory, ".architecture", "rules.yaml"));
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");
        loaded.Value.Name.Should().Be(directoryName);
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
    public void Lint_CommandIsRemoved_ShouldNotBeRegistered()
    {
        CliRootCommand.Create(_provider).Subcommands.Select(c => c.Name).Should().NotContain("lint");
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
    public void Rules_PrintsSelectorsAndRules()
    {
        WriteRules("""
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            rules:
              - id: SH-001
                because: The domain stays free of EF Core.
                forbid:
                  from:
                    layers: [Domain]
                  to:
                    namespaces: ["Microsoft.EntityFrameworkCore.**"]
                    types: ["**.DbContext"]
                exceptions:
                  - types: ["**.Bootstrap"]
                    because: The composition root wires everything.
              - id: SH-002
                because: Agents only use the runner.
                allowOnly:
                  from:
                    namespaces: ["**.Agents.**"]
                  to:
                    types: ["**.IRunner"]
            """);

        var (exitCode, stdout) = RunCapturingStdout("rules");

        exitCode.Should().Be(ExitCodes.Success);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain("  Domain [CA-LAYER-DOMAIN]: namespaces **.Domain.**; depends on nothing");
        lines.Should().Contain("  Application [CA-LAYER-APPLICATION]: namespaces **.Application.**; depends on Domain");
        lines.Should().Contain("  WebApi [CA-LAYER-WEBAPI]: namespaces **.WebApi.**; depends on Domain, Application, Infrastructure");
        var rules = lines.SkipWhile(l => !string.Equals(l, "Rules:", StringComparison.Ordinal)).Skip(1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)).ToArray();
        rules.Should().Equal(
            "  SH-001 forbid layers Domain -> namespaces Microsoft.EntityFrameworkCore.**; types **.DbContext",
            "    1 exception(s)",
            "    The domain stays free of EF Core.",
            "  SH-002 allowOnly namespaces **.Agents.** -> types **.IRunner",
            "    Agents only use the runner.");
    }

    [Fact]
    public void RulesCommand_ListsBannedPackagesAndUniqueNames()
    {
        WriteRules("""
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            rules:
              - id: SH-020
                because: JSON goes through System.Text.Json.
                bannedPackages:
                  packages: [Newtonsoft.Json, "Serilog.*"]
                  from: { assemblies: ["Shop.*"] }
              - id: SH-021
                because: Host types keep their own names.
                uniqueNames:
                  for: { namespaces: ["**.Web.**"] }
                  against: { layers: [Domain] }
              - id: SH-022
                because: The domain never writes to the console.
                forbid:
                  from: { layers: [Domain] }
                  to: { members: ["System.Console.*"] }
            """);

        var (exitCode, stdout) = RunCapturingStdout("rules");

        exitCode.Should().Be(ExitCodes.Success);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var rules = lines.SkipWhile(l => !string.Equals(l, "Rules:", StringComparison.Ordinal)).Skip(1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)).ToArray();
        rules.Should().Equal(
            "  SH-020 bannedPackages assemblies Shop.* -> packages Newtonsoft.Json, Serilog.*",
            "    JSON goes through System.Text.Json.",
            "  SH-021 uniqueNames namespaces **.Web.** -> against layers Domain",
            "    Host types keep their own names.",
            "  SH-022 forbid layers Domain -> members System.Console.*",
            "    The domain never writes to the console.");
    }

    [Fact]
    public void Rules_PrintsARequireRuleWithItsRequirements()
    {
        WriteRules("""
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            rules:
              - id: SH-010
                because: Repositories live in Infrastructure.
                require:
                  for: { interfaces: ["**.I*Repository"], attributes: ["**.Stored"] }
                  layer: Infrastructure
                  kind: class
                  accessibility: internal
                  implements: { types: ["**.IRepositoryContract"] }
                exceptions:
                  - types: ["**.Legacy"]
                    because: Old code.
            """);

        var (exitCode, stdout) = RunCapturingStdout("rules");

        exitCode.Should().Be(ExitCodes.Success);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var rules = lines.SkipWhile(l => !string.Equals(l, "Rules:", StringComparison.Ordinal)).Skip(1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)).ToArray();
        rules.Should().Equal(
            "  SH-010 require interfaces **.I*Repository; attributes **.Stored -> layer Infrastructure, kind class, internal, implements types **.IRepositoryContract",
            "    1 exception(s)",
            "    Repositories live in Infrastructure.");
    }

    [Fact]
    public void Rules_LayersSelectedByAssemblyOrNothing_PrintEachSelectorKind()
    {
        WriteRules("""
            schema: 2
            name: shop
            layers:
              - name: Data
                id: SH-LAYER-DATA
                because: Data access lives in one assembly.
                assemblies: ["Shop.Data"]
              - name: Mixed
                id: SH-LAYER-MIXED
                because: Mixed selection.
                namespaces: ["**.Mixed.**"]
                assemblies: ["Shop.Mixed"]
                allowedDependencies: [Data]
            """);

        var (exitCode, stdout) = RunCapturingStdout("rules");

        exitCode.Should().Be(ExitCodes.Success);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.SkipWhile(l => !string.Equals(l, "Layers:", StringComparison.Ordinal)).Skip(1).Take(2).Should().Equal(
            "  Data [SH-LAYER-DATA]: assemblies Shop.Data; depends on nothing",
            "  Mixed [SH-LAYER-MIXED]: namespaces **.Mixed.**; assemblies Shop.Mixed; depends on Data");
    }

    [Fact]
    public void Rules_WithoutRules_PrintsNoneUnderRules()
    {
        var (exitCode, stdout) = RunCapturingStdout("rules");

        exitCode.Should().Be(ExitCodes.Success);
        var lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.SkipWhile(l => !string.Equals(l, "Rules:", StringComparison.Ordinal)).Skip(1).First().Should().Be("  (none)");
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
    public void Sync_MalformedImportsFile_ReturnsErrorAndWritesStderr()
    {
        var importsPath = Path.Combine(_tempDir, ".architecture", "imports.yaml");
        File.WriteAllText(importsPath, "imports:\n  - name: \"broken\n");
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("sync").Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "an expected failure prints the message and the location, not a stack trace");
        lines[0].Should().StartWith($"Error: Imports file '{importsPath}' could not be parsed: ");
        lines[1].Should().StartWith("line ");
    }

    [Fact]
    public void Validate_RulesExtendUnknownBuiltin_ShouldReturnErrorAndWriteStderr()
    {
        WriteRules("""
            schema: 2
            name: shop
            extends:
              - builtin://nope
            """);
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("validate", FixtureProjects.Path("clean", "Shop.slnx")).Should().Be(ExitCodes.Error);
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
        WriteRules("schema: 2\nname: x\nlayers: [unclosed");
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        try
        {
            Run("validate", FixtureProjects.Path("clean", "Shop.slnx")).Should().Be(ExitCodes.Error);
        }
        finally
        {
            Console.SetError(originalError);
        }

        var lines = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("Error: Rule pack file '.architecture/rules.yaml' has 1 problem(s).");
        lines[1].Should().StartWith(".architecture/rules.yaml:4:1: ");
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
            Run("validate", FixtureProjects.Path("clean", "Shop.slnx")).Should().Be(ExitCodes.Error);
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
    public void Sync_MissingImportsFile_ReportsNotFoundWithThePath()
    {
        var importsPath = Path.Combine(_tempDir, ".architecture", "imports.yaml");
        var (exitCode, stderr) = RunCapturingStderr("sync");

        exitCode.Should().Be(ExitCodes.Error);
        var lines = stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Equal($"Error: Imports file '{importsPath}' not found.", importsPath);
    }

    [Fact]
    public void Sync_OneImportFails_PrintsFailedLineToStderrAndReturnsError()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, ".architecture", "imports.yaml"),
            """
            imports:
              - name: nuget-pack
                type: nuget
                url: https://example.com/pkg
                version: 1.0.0
            """);

        var (exitCode, stderr) = RunCapturingStderr("sync");

        exitCode.Should().Be(ExitCodes.Error);
        var lines = stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().MatchRegex(@"^  nuget-pack +FAILED: Unsupported import type 'nuget'\. Only 'git' is currently supported\.$");
        lines[^1].Should().Be("Done. 0 synced, 1 failed.");
    }

    [Fact]
    public void Sync_CacheDirectoryCannotBeCreated_ReportsUnavailableAndReturnsError()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, ".architecture", "imports.yaml"),
            """
            imports:
              - name: pack
                type: git
                url: https://example.com/pack.git
                version: ^1.0
            """);

        // A file where the cache directory's parent should be makes creating the cache fail.
        File.WriteAllText(Path.Combine(_tempDir, ".architecture", ".cache"), "not a directory");

        var (exitCode, stderr) = RunCapturingStderr("sync");

        exitCode.Should().Be(ExitCodes.Error);
        var lines = stderr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2, "an expected failure prints the message and the OS reason, not a stack trace");
        lines[0].Should().Be($"Error: Import cache directory '{Path.Combine(_tempDir, ".architecture", ".cache", "git")}' could not be created.");
    }

    [Fact]
    public void Sync_MultiLineDetail_IsIndentedOnEveryLine()
    {
        using var writer = new StringWriter();

        SyncCommand.WriteIndented(writer, "fatal: not a repository\r\nfatal: Could not read\n\nPlease check");

        var nl = Environment.NewLine;
        writer.ToString().Should().Be(
            $"    fatal: not a repository{nl}    fatal: Could not read{nl}{nl}    Please check{nl}");
    }

    private (int ExitCode, string Stdout) RunCapturingStdout(params string[] args)
    {
        var originalOut = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            return (Run(args), output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private (int ExitCode, string Stderr) RunCapturingStderr(params string[] args)
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);
        try
        {
            return (Run(args), error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task UnexpectedException_ReportsMessageThenStackTraceAndReturnsError()
    {
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        int exitCode;
        try
        {
            exitCode = await CliRootCommand.InvokeAsync(new ThrowingServiceProvider(), ["rules"]);
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

    [Fact]
    public async Task CancelledCommand_ReportsOneLineAndReturnsError()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);

        int exitCode;
        try
        {
            exitCode = await CliRootCommand.InvokeAsync(new CancellingServiceProvider(cancelled.Token), ["rules"]);
        }
        finally
        {
            Console.SetError(originalError);
        }

        exitCode.Should().Be(ExitCodes.Error);
        error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().Equal("Cancelled.");
    }

    private sealed class CancellingServiceProvider(CancellationToken token) : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new OperationCanceledException(token);
    }

    private sealed class ThrowingServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new InvalidOperationException("boom");
    }
}
