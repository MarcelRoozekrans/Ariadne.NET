using FluentAssertions;
using ArchGov.Core.CodeGeneration;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.Orchestration;

public class GenerateOrchestratorTests
{
    private static RulePack CreateTestPack() => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", AllowedDependencies = [] },
            new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
        ],
        ComponentRoles =
        [
            new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class },
            new ComponentRoleDefinition { Name = "CommandHandler", Layer = "Application", TypeKind = CSharpTypeKind.Class },
            new ComponentRoleDefinition { Name = "Repository", Layer = "Infrastructure", TypeKind = CSharpTypeKind.Class, RequiresInterface = true, InterfaceRole = "RepositoryInterface", Accessibility = "internal" },
            new ComponentRoleDefinition { Name = "RepositoryInterface", Layer = "Domain", TypeKind = CSharpTypeKind.Interface },
        ],
        NamingConventions = new NamingConventions
        {
            Namespace = "{SolutionName}.{Feature}.{Layer}",
            FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
        },
    };

    [Fact]
    public void Generate_ShouldReturnGeneratedFilesForValidComponents()
    {
        var pack = CreateTestPack();
        var orchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());

        var request = new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = "Entity", Name = "Order" },
                new ComponentRequest { RoleName = "CommandHandler", Name = "CreateOrderHandler" },
            ],
        };

        var result = orchestrator.Generate(pack, request);

        result.Success.Should().BeTrue();
        result.GeneratedFiles.Should().HaveCount(2);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ShouldReportErrorForUnknownRole()
    {
        var pack = CreateTestPack();
        var orchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());

        var request = new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = "NonExistentRole", Name = "Foo" },
            ],
        };

        var result = orchestrator.Generate(pack, request);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_GeneratedCodeShouldContainCorrectNamespace()
    {
        var pack = CreateTestPack();
        var orchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());

        var request = new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = "Entity", Name = "Order" },
            ],
        };

        var result = orchestrator.Generate(pack, request);

        result.GeneratedFiles[0].Namespace.Should().Be("MyApp.Orders.Domain");
        result.GeneratedFiles[0].Code.Should().Contain("MyApp.Orders.Domain");
    }
}
