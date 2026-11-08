using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.Tests.Analysis;
using Ariadne.NET.RuleLoader;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Tests.Integration;

/// <summary>
/// Runs the repository's own rule pack over small broken compilations, so every rule that
/// <c>.architecture/rules.yaml</c> adds is shown to fire and cannot pass vacuously.
/// </summary>
public sealed class RepoPackRuleTests
{
    private static readonly Lazy<RulePack> Pack = new(LoadRepoPack);

    private static RulePack LoadRepoPack()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "src", "Ariadne.NET.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        var loader = new YamlRulePackLoader();
        var loaded = loader.LoadFromFile(Path.Combine(dir!, ".architecture", "rules.yaml"));
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");
        var resolved = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger()).Resolve(loaded.Value);
        resolved.IsSuccess.Should().BeTrue(resolved.IsFailure ? $"{resolved.Error.Message} {resolved.Error.Detail}" : "");
        return resolved.Value;
    }

    private static AnalyzedProject Project(string assembly, string source, params ResolvedPackage[] packages) =>
        new(assembly, null, TestCompilation.Create(source).WithAssemblyName(assembly)) { Packages = packages };

    private static IReadOnlyList<ArchitectureViolation> Validate(params AnalyzedProject[] projects) =>
        new ArchitectureValidator().Validate(Pack.Value, projects).Violations
            .Where(v => v.RuleId.StartsWith("ARIADNE-", StringComparison.Ordinal) && !v.RuleId.StartsWith("ARIADNE-LAYER-", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void HostTypeCopyingACoreTypeName_BreaksTheUniqueNamesRule()
    {
        var violations = Validate(
            Project("Ariadne.NET", "namespace Ariadne.NET.Analysis { public class ErrorReport { } }"),
            Project("Ariadne.NET.Cli", "namespace Ariadne.NET.Cli { public class ErrorReport { } }"));

        var v = violations.Should().ContainSingle(x => x.Kind == ViolationKind.NameCollision).Subject;
        v.RuleId.Should().Be("ARIADNE-HOST-NAMES-OWN");
        v.SourceType.Should().Be("Ariadne.NET.Cli.ErrorReport");
        v.Target.Should().Be("Ariadne.NET.Analysis.ErrorReport");
    }

    [Fact]
    public void HostTypeWithItsOwnName_DoesNotBreakTheUniqueNamesRule()
    {
        Validate(
            Project("Ariadne.NET", "namespace Ariadne.NET.Analysis { public class ErrorReport { } }"),
            Project("Ariadne.NET.Cli", "namespace Ariadne.NET.Cli { public class HostOutput { } }"))
            .Should().BeEmpty();
    }

    [Fact]
    public void CoreCodeWritingToTheConsole_BreaksTheNoConsoleRule()
    {
        var violations = Validate(
            Project("Ariadne.NET", "namespace Ariadne.NET.Analysis { public class Reporter { public void Run() => System.Console.WriteLine(\"x\"); } }"));

        var v = violations.Should().ContainSingle().Subject;
        v.RuleId.Should().Be("ARIADNE-CORE-NO-CONSOLE");
        v.Kind.Should().Be(ViolationKind.ForbiddenDependency);
        v.SourceType.Should().Be("Ariadne.NET.Analysis.Reporter");
        v.Target.Should().Contain("System.Console.WriteLine");
    }

    [Fact]
    public void HostCodeWritingToTheConsole_IsAllowed()
    {
        Validate(
            Project("Ariadne.NET.Cli", "namespace Ariadne.NET.Cli { public class Out { public void Run() => System.Console.WriteLine(\"x\"); } }"))
            .Should().BeEmpty();
    }

    [Fact]
    public void ProjectReferencingNewtonsoftJson_BreaksTheBannedPackagesRule()
    {
        var violations = Validate(
            Project("Ariadne.NET.Cli", "namespace Ariadne.NET.Cli { public class A { } }", new ResolvedPackage("Newtonsoft.Json", "13.0.3", true, ["Newtonsoft.Json"])));

        var v = violations.Should().ContainSingle().Subject;
        v.RuleId.Should().Be("ARIADNE-NO-NEWTONSOFT-JSON");
        v.Kind.Should().Be(ViolationKind.BannedPackage);
        v.Target.Should().Be("Newtonsoft.Json");
    }

    [Fact]
    public void ProjectWithoutTheBannedPackage_PassesTheBannedPackagesRule()
    {
        Validate(
            Project("Ariadne.NET.Cli", "namespace Ariadne.NET.Cli { public class A { } }", new ResolvedPackage("YamlDotNet", "16.0.0", true, ["YamlDotNet"])))
            .Should().BeEmpty();
    }
}
