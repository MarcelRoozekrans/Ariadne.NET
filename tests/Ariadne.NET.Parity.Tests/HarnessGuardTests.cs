using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Testing;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Sdk;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

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

        // Even when both tools flag the Clean type, so the sets agree, the case fails and names the type and the case.
        var agree = () => ParityHarness.AssertAgrees(Case, ariadne, ariadne);
        agree.Should().Throw<XunitException>().Which.Message.Should().Contain(LeakyType).And.Contain(Case.Id);
    }

    [Fact]
    public void Gap_with_pack_rule_fails()
    {
        ParityHarness.PackHasRule(Kind, RuleId).Should().BeTrue();
        ParityHarness.PackHasRule(Kind, "PARITY-NO-SUCH-RULE").Should().BeFalse();

        var gap = () => ParityHarness.AssertGapHasNoRule(Case);
        gap.Should().Throw<XunitException>().Which.Message.Should().Contain(RuleId).And.Contain(Case.Id);
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
        agree.Should().Throw<XunitException>().Which.Message.Should().Contain(BrokenType).And.Contain(Case.Id);
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

        run.Should().Throw<ArchitectureCheckException>().Which.Message.Should().Contain(expected).And.Contain("bogus");
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

        project.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("the project did not load").And.Contain(Case.Id);
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

        project.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("SHOP-OTHER").And.Contain(Case.Id);
    }

    [Theory]
    [InlineData("Parity.PAre", "PAre", true)]
    [InlineData("Parity.PResideInAssembly.Other", "PResideInAssembly", true)]
    [InlineData("Parity.PAreAbstract", "PAre", false)]
    [InlineData("Parity.PAreAbstract.Other", "PAre", false)]
    public void Kind_assembly_is_the_kind_or_below_it(string assembly, string kind, bool expected)
    {
        // The ArchUnitNET load filter Parity.PAre*.dll also matches Parity.PAreAbstract.dll; this check rejects it.
        ParityHarness.IsKindAssembly(assembly, kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slice_projection_yields_full_namespaces(bool withPackages)
    {
        // The fixture has no cycle, but Broken depends on Target, so "should not depend on each other" fails the Broken
        // slice. Matching names it by the remainder, Broken, and the harness prepends the literal prefix;
        // MatchingWithPackages names it by its full namespace already. Both sides must read Parity.CNotDependOnAny.Broken.
        const string Pattern = "Parity.CNotDependOnAny.(*)";
        var slices = new ParityCase(
            "s-guard",
            Kind,
            "Parity.CNotDependOnAny",
            RuleId,
            Projection.Slices,
            _ => (withPackages ? Slices().MatchingWithPackages("Parity.CNotDependOnAny.(**)") : Slices().Matching(Pattern))
                .Should().NotDependOnEachOther(),
            Pattern);

        var archUnitNet = ParityHarness.ArchUnitNetFailures(slices);
        var ariadne = ParityHarness.FromReport(slices, ReportOf(BrokenType));

        archUnitNet.Should().BeEquivalentTo(["Parity.CNotDependOnAny.Broken"]);
        ariadne.Should().BeEquivalentTo(["Parity.CNotDependOnAny.Broken"]);
        ParityHarness.AssertAgrees(slices, archUnitNet, ariadne);
    }

    [Fact]
    public void Slice_outside_pattern_fails()
    {
        // Broken.OrderService lies in Parity.CNotDependOnAny.Broken itself, which no slice of the pattern below holds.
        var slices = new ParityCase(
            "s-guard-outside",
            Kind,
            "Parity.CNotDependOnAny",
            RuleId,
            Projection.Slices,
            _ => Slices().Matching("Parity.CNotDependOnAny.Broken.(*)").Should().NotDependOnEachOther(),
            "Parity.CNotDependOnAny.Broken.(*)");

        var project = () => ParityHarness.FromReport(slices, ReportOf(BrokenType));

        project.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(BrokenType).And.Contain("s-guard-outside");
    }

    private static ArchitectureReport ReportOf(string sourceType) => new()
    {
        Violations =
        [
            new ArchitectureViolation
            {
                RuleId = RuleId,
                Kind = ViolationKind.ForbiddenDependency,
                SourceType = sourceType,
                Message = "m",
            },
        ],
        Projects = [],
        TypesChecked = 1,
    };

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

                FixtureProjects.Dotnet("restore", copy.Project);
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
    }
}
