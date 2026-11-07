using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class ProjectRulesTests
{
    private static ProjectRules Create()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        return new ProjectRules(loader, new RulePackResolver(registry, loader, new RulePackMerger()));
    }

    [Fact]
    public void Loads_and_resolves_the_project_rules()
    {
        using var project = new TempProject();
        project.WriteRules();

        var result = Create().Load(project.Root);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Contain("shop");
        result.Value.Layers.Should().NotBeEmpty();
    }

    [Fact]
    public void A_missing_rules_file_is_not_found()
    {
        using var project = new TempProject();

        var result = Create().Load(project.Root);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Be("No .architecture/rules.yaml found. Run 'ariadne init' first.");
    }
}
