using FluentAssertions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Mcp.Tools;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class RuleToolsTests
{
    private readonly RuleResolutionHelper _helper;

    public RuleToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
    }

    [Fact]
    public void GetComponentRoles_ShouldReturnRolesForTemplate()
    {
        var result = RuleTools.GetComponentRoles(_helper, "clean-architecture");

        result.Should().Contain(r => r.Name == "Entity");
        result.Should().Contain(r => r.Name == "Repository");
    }

    [Fact]
    public void GetComponentRoles_CompositeTemplate_ShouldMergeRolesFromEachPack()
    {
        var result = RuleTools.GetComponentRoles(_helper, "clean-architecture+cqrs");

        result.Should().Contain(r => r.Name == "Entity");
        result.Should().Contain(r => r.Name == "Command");
    }

    [Fact]
    public void GetRules_ShouldReturnCodeFeatureRules()
    {
        var result = RuleTools.GetRules(_helper, "clean-architecture");

        result.Should().NotBeEmpty();
    }
}
