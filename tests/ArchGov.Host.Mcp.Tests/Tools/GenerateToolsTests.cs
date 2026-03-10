using FluentAssertions;
using ArchGov.Core.CodeGeneration;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

public class GenerateToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly GenerateOrchestrator _orchestrator;

    public GenerateToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
        _orchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());
    }

    [Fact]
    public void GenerateComponent_ShouldReturnGeneratedCode()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "Entity", name: "Order",
            solution: "MyApp", feature: "Orders",
            template: "clean-architecture");

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain("class Order");
        result.GeneratedFiles[0].Namespace.Should().Be("MyApp.Orders.Domain");
    }

    [Fact]
    public void GenerateComponent_UnknownRole_ShouldReturnError()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "NonExistent", name: "Foo",
            solution: "MyApp", feature: "Orders",
            template: "clean-architecture");

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
