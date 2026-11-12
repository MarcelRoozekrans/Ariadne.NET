using System.Diagnostics;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Testing;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Proves that the harness fails in the situations that would otherwise let a wrong case pass: an over-selecting rule, a
/// stale gap or divergence entry, a pack error and a skipped project.
/// </summary>
[Collection(ParityFixtureCollection.Name)]
public sealed class HarnessGuardTests
{
    private const string Kind = "CNotDependOnAny";
    private const string RuleId = "PARITY-CNOTDEPENDONANY";
    private const string BrokenType = "Parity.CNotDependOnAny.Broken.OrderService";
    private const string LeakyType = "Parity.CNotDependOnAny.Clean.LeakyService";

    public HarnessGuardTests() => FixtureProjects.EnsureParityBuilt();

    private static ParityCase Case => ParityCases.Get("c-not-depend-on-any");

    [Fact]
    public void Clean_flagged_fails_the_case()
    {
        // A temp copy whose Clean namespace holds a type that depends on Target; the kind's pack covers Clean as well, so
        // Ariadne flags it.
        using var copy = TempKind.Create(Kind, files: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Clean/LeakyService.cs"] = """
                using Parity.CNotDependOnAny.Target;

                namespace Parity.CNotDependOnAny.Clean;

                public class LeakyService
                {
                    public Repository? Store;
                }
                """,
        });
        var ariadne = ParityHarness.AriadneFailures(Case, copy.Project);
        ariadne.Should().BeEquivalentTo([BrokenType, LeakyType]);

        // Against ArchUnitNET's answer on the real fixture the sets differ.
        var differ = () => ParityHarness.AssertAgrees(Case, ParityHarness.ArchUnitNetFailures(Case), ariadne);
        differ.Should().Throw<Exception>();

        // Even when both tools flag the Clean type, the case fails, and says which type.
        var agree = () => ParityHarness.AssertAgrees(Case, ariadne, ariadne);
        agree.Should().Throw<Exception>().Which.Message.Should().Contain(LeakyType);
    }

    [Fact]
    public void Gap_with_pack_rule_fails()
    {
        ParityHarness.PackHasRule(Kind, RuleId).Should().BeTrue();
        ParityHarness.PackHasRule(Kind, "PARITY-NO-SUCH-RULE").Should().BeFalse();

        var gap = () => ParityHarness.AssertGapHasNoRule(Case);
        gap.Should().Throw<Exception>().Which.Message.Should().Contain(RuleId);
    }

    [Fact]
    public void Divergence_that_no_longer_differs_fails()
    {
        var recorded = new Divergence(
            Case.Id,
            new HashSet<string>(StringComparer.Ordinal) { BrokenType },
            new HashSet<string>(StringComparer.Ordinal),
            "test",
            "test");
        var broken = new HashSet<string>(StringComparer.Ordinal) { BrokenType };
        var empty = new HashSet<string>(StringComparer.Ordinal);

        // The recorded difference holds: no failure.
        var holds = () => ParityHarness.AssertDivergence(Case, recorded, broken, empty);
        holds.Should().NotThrow();

        // The tools now agree, so the empty differences no longer match the recorded one.
        var agree = () => ParityHarness.AssertDivergence(Case, recorded, broken, broken);
        agree.Should().Throw<Exception>();
    }

    [Fact]
    public void Pack_error_fails_with_message()
    {
        const string BadPack = """
            schema: 2
            name: parity-bad
            version: "1.0.0"
            author: fixture
            bogus: true
            """;
        using var copy = TempKind.Create(Kind, pack: BadPack);
        var expected = LoaderError(Path.Combine(copy.Folder, ".architecture", "rules.yaml"));

        var run = () => ParityHarness.AriadneFailures(Case, copy.Project);

        run.Should().Throw<ArchitectureCheckException>().Which.Message.Should().Contain(expected);
    }

    [Fact]
    public void Skipped_project_fails_with_reason()
    {
        // ArchitectureCheck reports a project it could not load as skipped instead of throwing. A real skipped project needs
        // an unloadable fixture, so this checks the report handling directly.
        var report = new ArchitectureReport
        {
            Violations = [],
            Projects = [],
            SkippedProjects = [new SkippedProject("Parity.CNotDependOnAny", "the project did not load")],
            TypesChecked = 0,
        };

        var project = () => ParityHarness.FromReport(Case, report);

        project.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("the project did not load");
    }

    [Fact]
    public void Foreign_rule_violation_fails()
    {
        var report = new ArchitectureReport
        {
            Violations =
            [
                new ArchitectureViolation
                {
                    RuleId = "SHOP-OTHER",
                    Kind = ViolationKind.ForbiddenDependency,
                    SourceType = BrokenType,
                    Message = "m",
                },
            ],
            Projects = [],
            TypesChecked = 1,
        };

        var project = () => ParityHarness.FromReport(Case, report);

        project.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("SHOP-OTHER");
    }

    private static string LoaderError(string packPath)
    {
        var services = new ServiceCollection().AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();
        var result = provider.GetRequiredService<IRulePackLoader>().LoadFromFile(packPath);
        result.IsFailure.Should().BeTrue();
        return result.Error.Message;
    }

    /// <summary>
    /// A copy of a kind folder under <see cref="Path.GetTempPath"/>, without its <c>bin</c> and <c>obj</c>, restored so
    /// MSBuildWorkspace can load it. An empty <c>Directory.Build.props</c> at the copy's root keeps MSBuild from importing
    /// anything above it, as <c>tests/fixtures/Directory.Build.props</c> does for the real fixtures. Deleted on dispose.
    /// </summary>
    private sealed class TempKind : IDisposable
    {
        private readonly string _root;

        private TempKind(string root, string folder, string project)
        {
            _root = root;
            Folder = folder;
            Project = project;
        }

        public string Folder { get; }

        public string Project { get; }

        public static TempKind Create(string kind, string? pack = null, IReadOnlyDictionary<string, string>? files = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "ariadne-parity-" + Guid.NewGuid().ToString("N"));
            var folder = Path.Combine(root, kind);
            var copy = new TempKind(root, folder, Path.Combine(folder, $"Parity.{kind}.csproj"));
            try
            {
                StrayFiles.AssertNoArchitectureFolderAbove(root);
                CopyFolder(Path.Combine(FixtureProjects.ParityRoot, kind), folder);
                File.WriteAllText(Path.Combine(root, "Directory.Build.props"), "<Project />");
                if (pack is not null)
                {
                    File.WriteAllText(Path.Combine(folder, ".architecture", "rules.yaml"), pack);
                }

                foreach (var (relative, content) in files ?? new Dictionary<string, string>(StringComparer.Ordinal))
                {
                    File.WriteAllText(Path.Combine([folder, .. relative.Split('/')]), content);
                }

                Restore(copy.Project);
                return copy;
            }
            catch
            {
                copy.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static void CopyFolder(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }

            foreach (var dir in Directory.EnumerateDirectories(source))
            {
                var name = Path.GetFileName(dir);
                if (name is "bin" or "obj")
                {
                    continue;
                }

                CopyFolder(dir, Path.Combine(target, name));
            }
        }

        private static void Restore(string project)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("restore");
            info.ArgumentList.Add(project);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start dotnet.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"dotnet restore {project} did not finish within 5 minutes.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"dotnet restore {project} exited with code {process.ExitCode}.{Environment.NewLine}{stdout.GetAwaiter().GetResult()}{stderr.GetAwaiter().GetResult()}");
            }
        }
    }
}
