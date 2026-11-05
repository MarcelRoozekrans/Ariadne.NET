using System.ComponentModel;
using System.Reflection;
using FluentAssertions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Mcp.Tools;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class TemplateToolsTests
{
    private readonly BuiltinPackRegistry _registry;

    public TemplateToolsTests()
    {
        _registry = new BuiltinPackRegistry(new YamlRulePackLoader());
    }

    [Fact]
    public void ListTemplates_ShouldReturnAllBuiltinPacks()
    {
        var result = TemplateTools.ListTemplates(_registry);

        result.Should().Contain("clean-architecture");
        result.Should().Contain("hexagonal-architecture");
        result.Should().HaveCountGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void GetLayers_WithExplicitTemplate_ShouldReturnLayers()
    {
        var loader = new YamlRulePackLoader();
        var helper = new RuleResolutionHelper(
            _registry,
            new RulePackResolver(_registry, loader, new RulePackMerger()),
            loader);

        var result = TemplateTools.GetLayers(helper, "clean-architecture");

        result.Should().Contain(l => l.Name == "Domain");
        result.Should().Contain(l => l.Name == "Application");
    }

    [Fact]
    public void GetLayers_ReturnsIdsAndSelectors()
    {
        var loader = new YamlRulePackLoader();
        var helper = new RuleResolutionHelper(
            _registry,
            new RulePackResolver(_registry, loader, new RulePackMerger()),
            loader);

        var domain = TemplateTools.GetLayers(helper, "clean-architecture").First(l => string.Equals(l.Name, "Domain", StringComparison.Ordinal));

        domain.Id.Should().Be("CA-LAYER-DOMAIN");
        domain.Namespaces.Should().Equal("**.Domain.**");
        domain.Because.Should().NotBeEmpty();
    }

    [Fact]
    public void GetLayers_IsDescribedWithIdSelectorsDependenciesAndBecause()
    {
        var attribute = typeof(TemplateTools).GetMethod(nameof(TemplateTools.GetLayers))!
            .GetCustomAttribute<DescriptionAttribute>()!;

        attribute.Description.Should().Be(
            "Get the layers of the resolved pack with their id, selectors, allowed dependencies and because.");
    }
}
