using FluentAssertions;
using Ariadne.NET.CodeGeneration;
using Ariadne.NET.Orchestration;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Mcp.Tools;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class GenerateToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly GenerateOrchestrator _orchestrator;
    private readonly WorkspaceOrchestrator _workspaceOrchestrator;

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
        _workspaceOrchestrator = new WorkspaceOrchestrator();
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

    [Fact]
    public void GenerateComponent_Repository_ShouldProduceInternalClass()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "Repository", name: "OrderRepository",
            solution: "MyApp", feature: "Orders",
            template: "clean-architecture");

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain("internal class OrderRepository");
        result.GeneratedFiles[0].Namespace.Should().Be("MyApp.Orders.Infrastructure");
    }

    [Fact]
    public void GenerateComponent_Controller_ShouldProduceWebApiLayer()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "Controller", name: "OrdersController",
            solution: "MyApp", feature: "Orders",
            template: "clean-architecture");

        result.Success.Should().BeTrue();
        result.GeneratedFiles[0].Namespace.Should().Be("MyApp.Orders.WebApi");
        result.GeneratedFiles[0].Code.Should().Contain("class OrdersController");
    }

    [Fact]
    public void GenerateComponent_HexagonalDrivingPort_ShouldProduceInterface()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "DrivingPort", name: "IProductService",
            solution: "MyApp", feature: "Products",
            template: "hexagonal-architecture");

        result.Success.Should().BeTrue();
        result.GeneratedFiles[0].Code.Should().Contain("interface");
        result.GeneratedFiles[0].Namespace.Should().Contain("Ports");
    }

    [Fact]
    public void GenerateComponent_EventDrivenProducer_ShouldGenerateCorrectly()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "EventProducer", name: "OrderEventProducer",
            solution: "MyApp", feature: "Orders",
            template: "event-driven");

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain("OrderEventProducer");
    }

    [Fact]
    public void GenerateComponent_VerticalSliceHandler_ShouldGenerateCorrectly()
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: "Handler", name: "CreateOrderHandler",
            solution: "MyApp", feature: "Orders",
            template: "vertical-slice");

        result.Success.Should().BeTrue();
        result.GeneratedFiles[0].Code.Should().Contain("CreateOrderHandler");
    }

    [Theory]
    [InlineData("clean-architecture", "Dto", "OrderDto")]
    [InlineData("clean-architecture", "DomainService", "PricingService")]
    [InlineData("clean-architecture", "Validator", "OrderValidator")]
    public void GenerateComponent_VariousRoles_ShouldSucceed(
        string template, string role, string name)
    {
        var result = GenerateTools.GenerateComponent(
            _helper, _orchestrator,
            role: role, name: name,
            solution: "MyApp", feature: "Orders",
            template: template);

        result.Success.Should().BeTrue($"role '{role}' on template '{template}' should generate");
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain(name);
    }

    [Fact]
    public void GenerateWorkspace_ShouldReturnProjectPaths()
    {
        var result = GenerateTools.GenerateWorkspace(
            _helper, _workspaceOrchestrator,
            solution: "MyApp", features: "Orders",
            template: "clean-architecture");

        result.ProjectPaths.Should().NotBeEmpty();
        result.Directories.Should().NotBeEmpty();
        result.ProjectPaths.Should().Contain(p => p.Contains("MyApp.Orders.Domain"));
    }

    [Fact]
    public void GenerateWorkspace_MultipleFeatures_ShouldReturnAllProjects()
    {
        var result = GenerateTools.GenerateWorkspace(
            _helper, _workspaceOrchestrator,
            solution: "MyApp", features: "Orders, Users",
            template: "clean-architecture");

        result.ProjectPaths.Should().Contain(p => p.Contains("MyApp.Orders.Domain"));
        result.ProjectPaths.Should().Contain(p => p.Contains("MyApp.Users.Domain"));
        result.ProjectPaths.Should().Contain(p => p.Contains("MyApp.Orders.Application"));
        result.ProjectPaths.Should().Contain(p => p.Contains("MyApp.Users.Application"));
    }
}
