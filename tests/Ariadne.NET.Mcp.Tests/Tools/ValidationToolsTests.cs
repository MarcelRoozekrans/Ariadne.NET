using Ariadne.NET.Analysis;
using Ariadne.NET.Mcp.Tools;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using Ariadne.NET.Workspaces;
using FluentAssertions;
using ModelContextProtocol;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class ValidationToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly SolutionValidator _validator;

    public ValidationToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
        _validator = new SolutionValidator(new SolutionLoader(), new ArchitectureValidator());
    }

    [Fact]
    public async Task ValidateSource_CleanFixture_IsValid()
    {
        FixtureProjects.EnsureRestored();

        var report = await ValidationTools.ValidateSource(
            _helper, _validator,
            path: FixtureProjects.Path("clean", "Shop.slnx"),
            template: "clean-architecture");

        report.IsValid.Should().BeTrue();
        report.TypesChecked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateSource_BrokenFixture_HasOneViolation()
    {
        FixtureProjects.EnsureRestored();

        var report = await ValidationTools.ValidateSource(
            _helper, _validator,
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
            _helper, _validator,
            path: missing,
            template: "clean-architecture");

        await act.Should().ThrowAsync<McpException>().WithMessage("Path '*' does not exist.*");
    }
}
