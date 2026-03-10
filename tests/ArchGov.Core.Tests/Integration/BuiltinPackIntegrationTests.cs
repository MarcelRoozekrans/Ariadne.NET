using FluentAssertions;
using ArchGov.Core.CodeGeneration;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;

namespace ArchGov.Core.Tests.Integration;

public class BuiltinPackIntegrationTests
{
    private readonly BuiltinPackRegistry _registry;
    private readonly GenerateOrchestrator _orchestrator;

    public BuiltinPackIntegrationTests()
    {
        var loader = new YamlRulePackLoader();
        _registry = new BuiltinPackRegistry(loader);
        _orchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());
    }

    [Theory]
    [InlineData("clean-architecture", "Entity", "Order")]
    [InlineData("clean-architecture", "Repository", "OrderRepository")]
    [InlineData("clean-architecture", "Controller", "OrdersController")]
    [InlineData("hexagonal-architecture", "Entity", "Product")]
    [InlineData("hexagonal-architecture", "DrivingPort", "IProductService")]
    [InlineData("hexagonal-architecture", "SecondaryAdapter", "ProductDbAdapter")]
    [InlineData("vertical-slice", "Request", "CreateOrderRequest")]
    [InlineData("vertical-slice", "Handler", "CreateOrderHandler")]
    [InlineData("vertical-slice", "Endpoint", "CreateOrderEndpoint")]
    [InlineData("event-driven", "Event", "OrderCreated")]
    [InlineData("event-driven", "EventProducer", "OrderEventProducer")]
    [InlineData("event-driven", "EventConsumer", "OrderEventConsumer")]
    [InlineData("modular-monolith", "ModuleContract", "IOrderModule")]
    [InlineData("modular-monolith", "ModuleService", "OrderModuleService")]
    [InlineData("modular-monolith", "ModuleEntity", "OrderEntity")]
    public void Generate_WithBuiltinPack_ShouldProduceValidCode(
        string packName, string roleName, string componentName)
    {
        var pack = _registry.GetPack(packName);

        var request = new GenerateRequest
        {
            SolutionName = "TestApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = roleName, Name = componentName },
            ],
        };

        var result = _orchestrator.Generate(pack, request);

        result.Success.Should().BeTrue($"pack '{packName}' role '{roleName}' should generate successfully");
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain(componentName);
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ddd-tactical", "Aggregate", "OrderAggregate")]
    [InlineData("ddd-tactical", "ValueObject", "Money")]
    [InlineData("ddd-tactical", "DomainEvent", "OrderPlaced")]
    [InlineData("ddd-tactical", "Factory", "OrderFactory")]
    [InlineData("ddd-tactical", "DomainException", "InsufficientFundsException")]
    public void Generate_WithComposablePack_ShouldProduceValidCode(
        string packName, string roleName, string componentName)
    {
        // Composable packs (like ddd-tactical, cqrs) need a base pack with layers
        var merger = new RulePackMerger();
        var basePack = _registry.GetPack("clean-architecture");
        var addOnPack = _registry.GetPack(packName);
        var merged = merger.Merge([basePack, addOnPack]);

        var request = new GenerateRequest
        {
            SolutionName = "TestApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = roleName, Name = componentName },
            ],
        };

        var result = _orchestrator.Generate(merged, request);

        result.Success.Should().BeTrue($"composable pack '{packName}' role '{roleName}' should generate successfully");
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain(componentName);
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Command", "CreateOrderCommand")]
    [InlineData("CommandHandler", "CreateOrderCommandHandler")]
    [InlineData("Query", "GetOrderQuery")]
    [InlineData("QueryHandler", "GetOrderQueryHandler")]
    [InlineData("QueryResult", "OrderDetails")]
    public void Generate_WithCqrsPack_ShouldProduceValidCode(
        string roleName, string componentName)
    {
        var merger = new RulePackMerger();
        var basePack = _registry.GetPack("clean-architecture");
        var cqrsPack = _registry.GetPack("cqrs");
        var merged = merger.Merge([basePack, cqrsPack]);

        var request = new GenerateRequest
        {
            SolutionName = "TestApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = roleName, Name = componentName },
            ],
        };

        var result = _orchestrator.Generate(merged, request);

        result.Success.Should().BeTrue($"CQRS role '{roleName}' should generate successfully");
        result.GeneratedFiles.Should().HaveCount(1);
        result.GeneratedFiles[0].Code.Should().Contain(componentName);
    }

    [Theory]
    [InlineData("clean-architecture")]
    [InlineData("hexagonal-architecture")]
    [InlineData("event-driven")]
    [InlineData("vertical-slice")]
    [InlineData("modular-monolith")]
    public void Pack_ShouldHaveLayersAndRoles(string packName)
    {
        var pack = _registry.GetPack(packName);

        pack.Name.Should().Be(packName);
        pack.Layers.Should().NotBeEmpty($"pack '{packName}' should define layers");
        pack.ComponentRoles.Should().NotBeEmpty($"pack '{packName}' should define component roles");
        pack.NamingConventions.Should().NotBeNull();
        pack.NamingConventions.Namespace.Should().NotBeNullOrEmpty();
        pack.NamingConventions.FilePath.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("cqrs")]
    [InlineData("ddd-tactical")]
    public void ComposablePack_ShouldHaveRolesButNoLayers(string packName)
    {
        var pack = _registry.GetPack(packName);

        pack.Name.Should().Be(packName);
        pack.Layers.Should().BeEmpty($"composable pack '{packName}' should not define its own layers");
        pack.ComponentRoles.Should().NotBeEmpty($"composable pack '{packName}' should define component roles");
    }
}
