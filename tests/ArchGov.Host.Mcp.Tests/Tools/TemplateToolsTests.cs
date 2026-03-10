using FluentAssertions;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

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
}
