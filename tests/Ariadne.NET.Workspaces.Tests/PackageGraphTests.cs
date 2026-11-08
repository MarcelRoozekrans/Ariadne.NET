using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Checks the package lists the loader reads from <c>project.assets.json</c>, and the bannedPackages rules that run over them.</summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class PackageGraphTests
{
    private const string Validation = "ZeroAlloc.Validation";
    private const string Pipeline = "ZeroAlloc.Pipeline";

    private readonly SolutionLoader _loader = new();

    public PackageGraphTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task DirectPackage_IsDirect()
    {
        using var loaded = await LoadAsync("broken");

        var package = Package(PackagesOf(loaded, "Pkg.Core"), Validation);

        package.Version.Should().Be("2.5.0");
        package.IsDirect.Should().BeTrue();
        package.Path.Should().Equal(Validation);
    }

    [Fact]
    public async Task TransitivePackage_PathNamesParent()
    {
        using var loaded = await LoadAsync("broken");

        var package = Package(PackagesOf(loaded, "Pkg.Core"), Pipeline);

        package.IsDirect.Should().BeFalse();
        package.Path.Should().Equal(Validation, Pipeline);
    }

    [Fact]
    public async Task PackageThroughProjectReference_PathNamesProject()
    {
        using var loaded = await LoadAsync("broken");

        var packages = PackagesOf(loaded, "Pkg.App");

        var validation = Package(packages, Validation);
        validation.IsDirect.Should().BeFalse();
        validation.Path.Should().Equal("Pkg.Core", Validation);
        Package(packages, Pipeline).Path.Should().Equal("Pkg.Core", Validation, Pipeline);
        Package(packages, "ZeroAlloc.Results").IsDirect.Should().BeTrue();
        packages.Should().NotContain(p => string.Equals(p.Id, "Pkg.Core", StringComparison.Ordinal), "a referenced project is not a package");
    }

    [Fact]
    public async Task PackageThroughProjectReference_IsAViolationForTheReferencingProject()
    {
        var pack = new RulePack
        {
            Name = "test",
            Rules =
            [
                new RuleDefinition
                {
                    Id = "TEST-NO-VALIDATION",
                    Because = "Test ban.",
                    Kind = RuleKind.BannedPackages,
                    From = new TargetSelector { Assemblies = ["Pkg.App"] },
                    Packages = [Validation],
                },
            ],
        };
        var validator = new SolutionValidator(_loader, new ArchitectureValidator());

        var result = await validator.ValidateAsync(pack, FixtureProjects.Path("packages", "broken", "Packages.slnx"));

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        var violation = result.Value.Violations.Should().ContainSingle().Subject;
        violation.Kind.Should().Be(ViolationKind.BannedPackage);
        violation.SourceType.Should().Be("Pkg.App");
        violation.Message.Should().Be(
            "Pkg.App references ZeroAlloc.Validation 2.5.0 through Pkg.Core, which rule 'TEST-NO-VALIDATION' bans");
    }

    [Fact]
    public async Task AnalyzerPackage_IsListed()
    {
        using var loaded = await LoadAsync("broken");

        var generator = Package(PackagesOf(loaded, "Pkg.Core"), "Meziantou.Analyzer");

        generator.IsDirect.Should().BeTrue();
        PackagesOf(loaded, "Pkg.App").Should().NotContain(p => string.Equals(p.Id, "Meziantou.Analyzer", StringComparison.Ordinal), "its PrivateAssets keep it from consumers");
    }

    [Fact]
    public async Task MultiTarget_EachFrameworkGetsItsOwnList()
    {
        using var loaded = await LoadAsync("broken");

        var multi = loaded.Projects.Where(p => string.Equals(p.Name, "Pkg.Multi", StringComparison.Ordinal)).ToList();

        multi.Select(p => p.TargetFramework).Should().BeEquivalentTo("net10.0", "net9.0");
        multi.First(p => string.Equals(p.TargetFramework, "net10.0", StringComparison.Ordinal)).Packages!.Select(p => p.Id).Should().Equal("ZeroAlloc.Results");
        multi.First(p => string.Equals(p.TargetFramework, "net9.0", StringComparison.Ordinal)).Packages!.Select(p => p.Id).Should().Equal("YamlDotNet");
    }

    [Fact]
    public async Task Clean_HasNoBannedPackage()
    {
        using var loaded = await LoadAsync("clean");

        loaded.Projects.Should().OnlyContain(p => p.Packages != null);
        loaded.Projects.SelectMany(p => p.Packages!).Should().NotContain(p => string.Equals(p.Id, Validation, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Daedalus_BannedPackageInDomain_FailsWithRuleId()
    {
        var report = await DaedalusFixtureTests.ValidateAsync("broken");

        var violation = Violation(report, "DAE-DOMAIN-NO-YAML");
        violation.Kind.Should().Be(ViolationKind.BannedPackage);
        violation.SourceType.Should().Be("Daedalus.Domain");
        violation.Target.Should().Be("YamlDotNet");
        violation.Message.Should().Be(
            "Daedalus.Domain references YamlDotNet 16.3.0 through Daedalus.Infrastructure, which rule 'DAE-DOMAIN-NO-YAML' bans");
        Violation(report, "DAE-AGENTS-NO-STATE-MACHINE").Message.Should().Be(
            "Daedalus.Agents references ZeroAlloc.StateMachine 1.5.3 through ZeroAlloc.Resilience, which rule 'DAE-AGENTS-NO-STATE-MACHINE' bans");
        Violation(report, "DAE-APP-NO-VALIDATION-LIB").Message.Should().Be(
            "Daedalus.Application references ZeroAlloc.Validation 2.5.0 directly, which rule 'DAE-APP-NO-VALIDATION-LIB' bans");
    }

    [Fact]
    public async Task Daedalus_Clean_Passes()
    {
        var report = await DaedalusFixtureTests.ValidateAsync("clean");

        report.Violations.Should().BeEmpty();
    }

    private static ResolvedPackage Package(IReadOnlyList<ResolvedPackage> packages, string id)
    {
        var found = packages.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        found.Should().NotBeNull($"the project resolves {id}");
        return found!;
    }

    private static ArchitectureViolation Violation(ArchitectureReport report, string ruleId)
    {
        var found = report.Violations.Where(v => string.Equals(v.RuleId, ruleId, StringComparison.Ordinal)).ToList();
        found.Should().ContainSingle();
        return found[0];
    }

    private static IReadOnlyList<ResolvedPackage> PackagesOf(LoadedSolution loaded, string project)
    {
        var found = loaded.Projects.First(p => string.Equals(p.Name, project, StringComparison.Ordinal));
        found.Packages.Should().NotBeNull();
        return found.Packages!;
    }

    private async Task<LoadedSolution> LoadAsync(string variant)
    {
        var result = await _loader.LoadAsync(FixtureProjects.Path("packages", variant, "Packages.slnx")).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
