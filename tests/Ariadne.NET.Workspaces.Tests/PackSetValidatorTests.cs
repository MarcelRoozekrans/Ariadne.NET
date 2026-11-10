using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces.Tests;

[Collection(FixtureSolutionCollection.Name)]
public sealed class PackSetValidatorTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ariadne-packset-").FullName;

    public PackSetValidatorTests() => FixtureProjects.EnsureRestored();

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public async Task TwoPacks_ReportOnlyTheBrokenPacksViolations()
    {
        var path = FixtureProjects.Path("twopacks", "Shop.slnx");

        PackDiscovery.Find(path).Value.ProjectRoot.Should().Be(FixtureProjects.Path("twopacks"));
        var result = await Create(new CountingLoader()).ValidateAsync(path);

        result.IsSuccess.Should().BeTrue(Describe(result));
        var violation = result.Value.Violations.Should().ContainSingle().Which;
        violation.Pack.Should().Be("broken-pack");
        violation.RuleId.Should().Be("SHOP-DOMAIN-NO-INFRASTRUCTURE");
        violation.Because.Should().Contain("storage can change");
        violation.SourceType.Should().Be("Shop.Domain.Order");
    }

    [Fact]
    public async Task CleanPackAlone_Passes()
    {
        var root = Workspace(("a-clean.yaml", File.ReadAllText(FixtureProjects.Path("twopacks", ".architecture", "a-clean.yaml"))));

        var result = await Create(new CountingLoader()).ValidateAsync(root);

        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SolutionIsLoadedOnce_AndTypesCheckedIsNotDoubled()
    {
        var loader = new CountingLoader();
        var path = FixtureProjects.Path("twopacks", "Shop.slnx");

        var result = await Create(loader).ValidateAsync(path);

        loader.Calls.Should().Be(1);
        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value.TypesChecked.Should().Be(2);
        result.Value.Projects.Select(p => p.Name).Should().BeEquivalentTo("Shop.Domain", "Shop.Infrastructure");
    }

    [Fact]
    public async Task PackExtendingAnUnknownBuiltin_IsInvalid_NotNotFound()
    {
        var root = Workspace(("rules.yaml", Pack("broken") + "extends:\n  - builtin://nope\n"));

        var result = await Create(new CountingLoader()).ValidateAsync(root);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("rules.yaml").And.Contain("nope");
    }

    [Fact]
    public async Task DuplicatePackName_IsInvalid_AndNamesBothFiles()
    {
        var root = Workspace(("one.yaml", Pack("same")), ("two.yaml", Pack("same")));

        var result = await Create(new CountingLoader()).ValidateAsync(root);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("one.yaml").And.Contain("two.yaml").And.Contain("same").And.EndWith(".");
    }

    [Fact]
    public async Task DuplicatePackName_DifferingOnlyInCase_IsInvalid()
    {
        var root = Workspace(("one.yaml", Pack("Shop")), ("two.yaml", Pack("shop")));

        var result = await Create(new CountingLoader()).ValidateAsync(root);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("one.yaml").And.Contain("two.yaml");
    }

    [Fact]
    public async Task UnloadablePack_FailsWithTheFileNameFirst()
    {
        var root = Workspace(("a-clean.yaml", Pack("clean-pack")), ("b-bad.yaml", "schema: 2\nnope: true\n"));

        var result = await Create(new CountingLoader()).ValidateAsync(root);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().StartWith("b-bad.yaml: ");
    }

    [Fact]
    public async Task NoArchitectureFolder_IsNotFound()
    {
        var empty = Directory.CreateDirectory(Path.Combine(_temp, "nopacks")).FullName;
        var solution = Path.Combine(empty, "Shop.slnx");
        File.WriteAllText(solution, "<Solution />");

        var result = await Create(new CountingLoader()).ValidateAsync(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
    }

    [Fact]
    public async Task BannedPackagesCoverageGapInSecondPack_FailsTheCheck()
    {
        var banning = Pack("ban-pack") + """

            rules:
              - id: SHOP-BAN
                because: Test ban.
                bannedPackages:
                  packages: ["Anything"]
                  from:
                    assemblies: ["Shop.Domain"]
            """;
        var root = Workspace(("a-clean.yaml", Pack("clean-pack")), ("b-ban.yaml", banning));

        var result = await Create(new CountingLoader(dropPackages: true)).ValidateAsync(root);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("SHOP-BAN").And.Contain("dotnet restore");
    }

    [Fact]
    public void AddAriadneNETWorkspaces_ResolvesThePackSetValidator()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPackSetValidator>().Should().BeOfType<PackSetValidator>();
        provider.GetRequiredService<IPackSetValidator>().Should().BeSameAs(provider.GetRequiredService<IPackSetValidator>());
    }

    private static PackSetValidator Create(ISolutionLoader loader)
    {
        var packs = new YamlRulePackLoader();
        return new PackSetValidator(
            loader,
            new ArchitectureValidator(),
            packs,
            new RulePackResolver(new BuiltinPackRegistry(packs), packs, new RulePackMerger()));
    }

    private static string Pack(string name) =>
        $"schema: 2\nname: {name}\nversion: \"1.0.0\"\nauthor: test\n";

    // A temp solution folder with an .architecture folder; the counting loader ignores the solution's content and loads the twopacks fixture.
    private string Workspace(params (string File, string Content)[] packs)
    {
        var architecture = Directory.CreateDirectory(Path.Combine(_temp, ".architecture")).FullName;
        foreach (var (file, content) in packs)
        {
            File.WriteAllText(Path.Combine(architecture, file), content);
        }

        var solution = Path.Combine(_temp, "Shop.slnx");
        File.WriteAllText(solution, "<Solution />");
        return solution;
    }

    private static string Describe<T>(Result<T, AriadneError> result) =>
        result.IsFailure ? $"{result.Error.Message} {result.Error.Detail}" : "";

    private sealed class CountingLoader(bool dropPackages = false) : ISolutionLoader
    {
        private readonly SolutionLoader _inner = new();

        public int Calls { get; private set; }

        public async Task<Result<LoadedSolution, AriadneError>> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            Calls++;
            var real = FixtureProjects.Path("twopacks", "Shop.slnx");
            var loaded = await _inner.LoadAsync(real, cancellationToken).ConfigureAwait(false);
            if (loaded.IsFailure || !dropPackages)
            {
                return loaded;
            }

            using var solution = loaded.Value;
            var stripped = solution.Projects.Select(p => p with { Packages = null }).ToList();
            return Result<LoadedSolution, AriadneError>.Success(new LoadedSolution(solution.RootDirectory, stripped, solution.SkippedProjects, null));
        }
    }
}
