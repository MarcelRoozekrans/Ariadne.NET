using Ariadne.NET.Abstractions;
using FluentAssertions;
using Ariadne.NET.CodeGeneration;
using Ariadne.NET.Orchestration;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;

namespace Ariadne.NET.Tests.Integration;

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
    [InlineData("onion-architecture", "Entity", "Order")]
    [InlineData("onion-architecture", "RepositoryInterface", "IOrderRepository")]
    [InlineData("onion-architecture", "ApplicationService", "OrderService")]
    [InlineData("onion-architecture", "Controller", "OrdersController")]
    [InlineData("onion-architecture", "ViewModel", "OrderViewModel")]
    [InlineData("layered", "Controller", "OrdersController")]
    [InlineData("layered", "Service", "OrderService")]
    [InlineData("layered", "Repository", "OrderRepository")]
    [InlineData("layered", "DataModel", "OrderDataModel")]
    [InlineData("layered", "ApiModel", "CreateOrderApiModel")]
    [InlineData("microservice", "Entity", "Order")]
    [InlineData("microservice", "Aggregate", "OrderAggregate")]
    [InlineData("microservice", "Handler", "CreateOrderHandler")]
    [InlineData("microservice", "Controller", "OrdersController")]
    [InlineData("microservice", "IntegrationEvent", "OrderCreatedIntegrationEvent")]
    [InlineData("microservice", "MessagePublisher", "OrderMessagePublisher")]
    public void Generate_WithBuiltinPack_ShouldProduceValidCode(
        string packName, string roleName, string componentName)
    {
        var pack = _registry.GetPack(packName).Value;

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
        var basePack = _registry.GetPack("clean-architecture").Value;
        var addOnPack = _registry.GetPack(packName).Value;
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
        var basePack = _registry.GetPack("clean-architecture").Value;
        var cqrsPack = _registry.GetPack("cqrs").Value;
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
    [InlineData("onion-architecture")]
    [InlineData("layered")]
    [InlineData("microservice")]
    public void Pack_ShouldHaveLayersAndRoles(string packName)
    {
        var pack = _registry.GetPack(packName).Value;

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
        var pack = _registry.GetPack(packName).Value;

        pack.Name.Should().Be(packName);
        pack.Layers.Should().BeEmpty($"composable pack '{packName}' should not define its own layers");
        pack.ComponentRoles.Should().NotBeEmpty($"composable pack '{packName}' should define component roles");
    }

    [Fact]
    public void OnionArchitecture_ShouldHaveFiveLayers()
    {
        var pack = _registry.GetPack("onion-architecture").Value;

        pack.Layers.Should().HaveCount(5);
        pack.Layers.Select(l => l.Name).Should().BeEquivalentTo(
            ["DomainModel", "DomainServices", "ApplicationServices", "Infrastructure", "Presentation"]);
    }

    private static LayerDefinition Layer(RulePack pack, string name) =>
        pack.Layers.Should().ContainSingle(l => string.Equals(l.Name, name, StringComparison.Ordinal)).Subject;

    [Fact]
    public void OnionArchitecture_DomainModel_ShouldHaveNoDependencies()
    {
        var pack = _registry.GetPack("onion-architecture").Value;
        var domainModel = Layer(pack, "DomainModel");

        domainModel.AllowedDependencies.Should().BeEmpty();
    }

    [Fact]
    public void OnionArchitecture_Presentation_ShouldNotDependOnInfrastructure()
    {
        var pack = _registry.GetPack("onion-architecture").Value;
        var presentation = Layer(pack, "Presentation");

        presentation.AllowedDependencies.Should().NotContain("Infrastructure");
    }

    [Fact]
    public void Layered_ShouldHaveThreeLayers()
    {
        var pack = _registry.GetPack("layered").Value;

        pack.Layers.Should().HaveCount(3);
        pack.Layers.Select(l => l.Name).Should().BeEquivalentTo(
            ["Presentation", "Business", "DataAccess"]);
    }

    [Fact]
    public void Layered_ShouldEnforceTopDownDependencies()
    {
        var pack = _registry.GetPack("layered").Value;

        var presentation = Layer(pack, "Presentation");
        var business = Layer(pack, "Business");
        var dataAccess = Layer(pack, "DataAccess");

        // Top-down only: Presentation -> Business -> DataAccess
        presentation.AllowedDependencies.Should().BeEquivalentTo(["Business"]);
        business.AllowedDependencies.Should().BeEquivalentTo(["DataAccess"]);
        dataAccess.AllowedDependencies.Should().BeEmpty();
    }

    [Fact]
    public void Microservice_ShouldHaveFiveLayers()
    {
        var pack = _registry.GetPack("microservice").Value;

        pack.Layers.Should().HaveCount(5);
        pack.Layers.Select(l => l.Name).Should().BeEquivalentTo(
            ["Domain", "Application", "Infrastructure", "Api", "Contracts"]);
    }

    [Fact]
    public void Microservice_Contracts_ShouldHaveNoDependencies()
    {
        var pack = _registry.GetPack("microservice").Value;
        var contracts = Layer(pack, "Contracts");

        contracts.AllowedDependencies.Should().BeEmpty(
            "Contracts is the public API surface — it must not depend on internal layers");
    }

    [Fact]
    public void Microservice_Domain_ShouldNotDependOnContracts()
    {
        var pack = _registry.GetPack("microservice").Value;
        var domain = Layer(pack, "Domain");

        domain.AllowedDependencies.Should().BeEmpty(
            "Domain is the innermost layer and should have no dependencies");
    }
}
