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

        // The clean fixture has a Domain and a WebApi project only, and a layer that covers no type fails the run, so it is
        // validated against a pack of those two layers and not the four of clean-architecture.
        var report = await ValidationTools.ValidateSource(
            new FixedPackHelper(new RulePack
            {
                Name = "shop",
                Layers =
                [
                    new LayerDefinition { Name = "Domain", Id = "T-DOMAIN", Because = "The domain depends on nothing.", Namespaces = ["**.Domain.**"] },
                    new LayerDefinition { Name = "WebApi", Id = "T-WEBAPI", Because = "The API composes the domain.", Namespaces = ["**.WebApi.**"], AllowedDependencies = ["Domain"] },
                ],
            }),
            _validator,
            path: FixtureProjects.Path("clean", "Shop.slnx"));

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

    private sealed class FixedPackHelper(RulePack pack) : IRuleResolutionHelper
    {
        public Result<RulePack, AriadneError> Resolve(string? template) => Result<RulePack, AriadneError>.Success(pack);
    }
}
