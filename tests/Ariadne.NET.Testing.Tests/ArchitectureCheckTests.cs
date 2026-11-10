using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Testing.Tests;

public sealed class ArchitectureCheckTests : IDisposable
{
    // The clean fixture has no .architecture folder of its own, so in place it would be checked against this repository's
    // pack, whose layers cover none of its types. The tests check a copy that carries a pack written for it.
    private const string CleanPack = """
        schema: 2
        name: shop
        version: "1.0.0"
        author: test

        layers:
          - name: Domain
            id: SHOP-LAYER-DOMAIN
            because: The domain holds business rules only.
            assemblies: ["Shop.Domain"]
            allowedDependencies: []
          - name: WebApi
            id: SHOP-LAYER-WEBAPI
            because: The web API serves the domain.
            assemblies: ["Shop.WebApi"]
            allowedDependencies: [Domain]
        """;

    private readonly string _temp = Directory.CreateTempSubdirectory("ariadne-check-").FullName;

    public ArchitectureCheckTests() => FixtureProjects.EnsureRestored();

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void AssertValid_CleanSolution_Passes()
    {
        var solution = CleanCopy();

        var act = () => ArchitectureCheck.AssertValid(solution);

        act.Should().NotThrow();
    }

    [Fact]
    public void Run_CleanSolution_ReturnsTheReport()
    {
        var report = ArchitectureCheck.Run(CleanCopy());

        report.IsValid.Should().BeTrue();
        report.TypesChecked.Should().Be(2);
        report.Projects.Select(p => p.Name).Should().BeEquivalentTo("Shop.Domain", "Shop.WebApi");
    }

    [Fact]
    public void AssertValid_TwoPacks_ThrowsWithRuleIdPackAndBecause()
    {
        var act = () => ArchitectureCheck.AssertValid(FixtureProjects.Path("twopacks", "Shop.slnx"));

        var thrown = act.Should().Throw<ArchitectureViolationException>().Which;
        thrown.Message.Should().Contain("SHOP-DOMAIN-NO-INFRASTRUCTURE")
            .And.Contain("broken-pack")
            .And.Contain("storage can change without touching business rules")
            .And.Contain("Shop.Domain/Order.cs:");
        thrown.Report.Violations.Should().ContainSingle().Which.Pack.Should().Be("broken-pack");
        thrown.Should().BeAssignableTo<ArchitectureCheckException>();
    }

    [Fact]
    public void Run_TwoPacks_ReturnsTheViolationsWithoutThrowing()
    {
        var report = ArchitectureCheck.Run(FixtureProjects.Path("twopacks", "Shop.slnx"));

        report.IsValid.Should().BeFalse();
        report.Violations.Should().ContainSingle().Which.RuleId.Should().Be("SHOP-DOMAIN-NO-INFRASTRUCTURE");
    }

    [Fact]
    public async Task AssertValidAsync_TwoPacks_Throws()
    {
        var act = () => ArchitectureCheck.AssertValidAsync(FixtureProjects.Path("twopacks", "Shop.slnx"));

        await act.Should().ThrowAsync<ArchitectureViolationException>();
    }

    [Fact]
    public async Task RunAsync_CleanSolution_ReturnsTheReport()
    {
        var report = await ArchitectureCheck.RunAsync(CleanCopy());

        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Run_NoPacks_ThrowsCheckException_NamingTheMissingFolder()
    {
        var solution = Path.Combine(_temp, "Shop.slnx");
        File.WriteAllText(solution, "<Solution />");
        StrayFiles.AssertNoArchitectureFolderAbove(_temp);

        var act = () => ArchitectureCheck.Run(solution);

        act.Should().Throw<ArchitectureCheckException>()
            .Which.Should().NotBeOfType<ArchitectureViolationException>()
            .And.Subject.As<Exception>().Message.Should().Contain(".architecture");
    }

    [Fact]
    public void Run_MissingSolutionPath_ThrowsCheckException()
    {
        var act = () => ArchitectureCheck.Run(Path.Combine(_temp, "Missing.slnx"));

        act.Should().Throw<ArchitectureCheckException>().Which.Message.Should().Contain("Missing.slnx");
    }

    [Fact]
    public async Task NoPath_NoSolutionAboveTheStartFolder_ThrowsCheckException()
    {
        var start = Directory.CreateDirectory(Path.Combine(_temp, "bin", "Debug")).FullName;
        StrayFiles.AssertNoSolutionAbove(start);

        var act = () => ArchitectureCheck.RunFromAsync(null, start, CancellationToken.None);

        (await act.Should().ThrowAsync<ArchitectureCheckException>())
            .Which.Message.Should().Contain("No .sln or .slnx file").And.Contain(start);
    }

    [Fact]
    public async Task NoPath_TwoSolutionsInTheNearestFolder_ThrowsCheckException_NamingThem()
    {
        // The clean fixture holds both Shop.sln and Shop.slnx.
        var start = FixtureProjects.Path("clean", "Shop.Domain");

        var act = () => ArchitectureCheck.AssertValidFromAsync(null, start, CancellationToken.None);

        (await act.Should().ThrowAsync<ArchitectureCheckException>())
            .Which.Message.Should().Contain("Shop.sln").And.Contain("Shop.slnx");
    }

    [Fact]
    public async Task NoPath_FromTheSourceFolder_FindsThisRepositorysSolution_AndPassesItsOwnPack()
    {
        // The public overloads start the walk-up at AppContext.BaseDirectory. From this project's output folder,
        // tests/Ariadne.NET.Testing.Tests/bin/..., the walk-up cannot reach this repository's solution: it is
        // src/Ariadne.NET.slnx, and src/ is not an ancestor of the output folder. So the default start folder is not run
        // from bin; the same code path is started from src/, which finds src/Ariadne.NET.slnx and, by walking up from it,
        // the repository's .architecture/rules.yaml.
        var source = Path.Combine(Path.GetDirectoryName(FixtureProjects.Root)!, "..", "src");

        var report = await ArchitectureCheck.RunFromAsync(null, Path.GetFullPath(source), CancellationToken.None);

        report.Violations.Should().BeEmpty(string.Join(Environment.NewLine, report.Violations.Select(v => $"{v.RuleId} {v.SourceType} -> {v.Target}")));
        report.Projects.Select(p => p.Name).Should().Contain("Ariadne.NET.Testing");
    }

    // Copies the restored clean fixture, obj folders included, next to a pack written for it.
    private string CleanCopy()
    {
        var source = FixtureProjects.Path("clean");
        foreach (var project in new[] { "Shop.Domain", "Shop.WebApi" })
        {
            CopyFolder(Path.Combine(source, project), Path.Combine(_temp, project));
        }

        File.Copy(Path.Combine(source, "Shop.slnx"), Path.Combine(_temp, "Shop.slnx"));
        var architecture = Directory.CreateDirectory(Path.Combine(_temp, ".architecture")).FullName;
        File.WriteAllText(Path.Combine(architecture, "shop.yaml"), CleanPack);
        return Path.Combine(_temp, "Shop.slnx");
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
