using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Mcp.Tools;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using Ariadne.NET.Workspaces;
using FluentAssertions;
using ModelContextProtocol;
using ZeroAlloc.Results;

namespace Ariadne.NET.Mcp.Tests.Tools;

[Collection(FixtureSolutionCollection.Name)]
public class ValidationToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly SolutionValidator _validator;
    private readonly PackSetValidator _packSet;

    public ValidationToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
        _validator = new SolutionValidator(new SolutionLoader(), new ArchitectureValidator());
        _packSet = new PackSetValidator(new SolutionLoader(), new ArchitectureValidator(), loader, new RulePackResolver(registry, loader, new RulePackMerger()));
    }

    [Fact]
    public async Task ValidateSource_CleanFixture_IsValid()
    {
        FixtureProjects.EnsureRestored();

        // The fourlayer fixture has a type in each of the four clean-architecture layers.
        var report = await ValidationTools.ValidateSource(
            _helper, _validator, _packSet,
            path: FixtureProjects.Path("fourlayer", "Shop.csproj"),
            template: "clean-architecture");

        report.IsValid.Should().BeTrue();
        report.TypesChecked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateSource_BrokenFixture_HasOneViolation()
    {
        FixtureProjects.EnsureRestored();

        var report = await ValidationTools.ValidateSource(
            _helper, _validator, _packSet,
            path: FixtureProjects.Path("broken", "Shop.slnx"),
            template: "clean-architecture");

        report.IsValid.Should().BeFalse();
        report.Violations.Should().ContainSingle(v => v.Kind == ViolationKind.DependencyNotAllowed);
    }

    [Fact]
    public async Task ValidateSource_MissingPath_IsToolError()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ariadne-missing-{Guid.NewGuid():N}", "Nope.slnx");

        var act = () => ValidationTools.ValidateSource(
            _helper, _validator, _packSet,
            path: missing,
            template: "clean-architecture");

        await act.Should().ThrowAsync<McpException>().WithMessage("Path '*' does not exist.*");
    }

    [Fact]
    public async Task ValidateSource_WithoutTemplate_ChecksEveryPackLikeThePackSetValidator()
    {
        FixtureProjects.EnsureRestored();
        var path = FixtureProjects.Path("twopacks", "Shop.slnx");

        var report = await ValidationTools.ValidateSource(_helper, _validator, _packSet, path);

        var expected = (await _packSet.ValidateAsync(path)).Value;
        expected.Violations.Should().NotBeEmpty();
        report.Violations.Select(v => (v.RuleId, v.Pack, v.SourceType)).Should().BeEquivalentTo(expected.Violations.Select(v => (v.RuleId, v.Pack, v.SourceType)));
        report.Violations.Should().OnlyContain(v => v.Pack == "broken-pack");
    }

    [Fact]
    public async Task ValidateSource_WithTemplate_KeepsTheSinglePackPath()
    {
        FixtureProjects.EnsureRestored();

        var report = await ValidationTools.ValidateSource(
            _helper, _validator, new ThrowingPackSet(), FixtureProjects.Path("broken", "Shop.slnx"), template: "clean-architecture");

        report.Violations.Should().ContainSingle(v => v.Kind == ViolationKind.DependencyNotAllowed);
    }

    [Fact]
    public async Task ValidateSource_WithoutTemplateAndNoPacks_FallsBackToTheDefaultPack()
    {
        FixtureProjects.EnsureRestored();
        var notFound = new FixedPackSet(Result<ArchitectureReport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.NotFound, "No packs.")));

        var report = await ValidationTools.ValidateSource(_helper, _validator, notFound, FixtureProjects.Path("fourlayer", "Shop.csproj"));

        report.IsValid.Should().BeTrue();
        report.TypesChecked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateSource_WithoutTemplate_SurfacesOtherPackSetErrorsAsToolErrors()
    {
        var invalid = new FixedPackSet(Result<ArchitectureReport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "A pack is bad.")));

        var act = () => ValidationTools.ValidateSource(_helper, _validator, invalid, "x.slnx");

        await act.Should().ThrowAsync<McpException>().WithMessage("A pack is bad.*");
    }

    [Fact]
    public async Task ValidateSource_WithoutTemplate_MissingPath_IsStillToolError()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ariadne-missing-{Guid.NewGuid():N}", "Nope.slnx");

        var act = () => ValidationTools.ValidateSource(_helper, _validator, _packSet, missing);

        await act.Should().ThrowAsync<McpException>().WithMessage("Path '*' does not exist.*");
    }

    [Fact]
    public async Task ValidateSource_WithoutTemplate_PackExtendingAnUnknownBuiltin_IsToolError_NotAFallback()
    {
        var temp = Directory.CreateTempSubdirectory("ariadne-mcp-badpack-").FullName;
        try
        {
            var architecture = Directory.CreateDirectory(Path.Combine(temp, ".architecture")).FullName;
            File.WriteAllText(
                Path.Combine(architecture, "rules.yaml"),
                "schema: 2\nname: broken\nversion: \"1.0.0\"\nauthor: test\nextends:\n  - builtin://nope\n");
            var solution = Path.Combine(temp, "Shop.slnx");
            File.WriteAllText(solution, "<Solution />");

            var act = () => ValidationTools.ValidateSource(_helper, _validator, _packSet, solution);

            await act.Should().ThrowAsync<McpException>().WithMessage("*nope*");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private sealed class FixedPackSet(Result<ArchitectureReport, AriadneError> result) : IPackSetValidator
    {
        public Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class ThrowingPackSet : IPackSetValidator
    {
        public Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(string path, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The pack set must not be used with a template.");
    }
}
