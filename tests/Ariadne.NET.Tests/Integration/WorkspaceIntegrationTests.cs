using FluentAssertions;
using Ariadne.NET.CodeGeneration;
using Ariadne.NET.Orchestration;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Validation;

namespace Ariadne.NET.Tests.Integration;

/// <summary>
/// Integration tests that exercise the full workspace pipeline:
/// scaffold workspace → generate components → validate structure.
/// </summary>
public sealed class WorkspaceIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BuiltinPackRegistry _registry;
    private readonly WorkspaceOrchestrator _workspaceOrchestrator;
    private readonly GenerateOrchestrator _generateOrchestrator;
    private readonly Linter _linter;

    public WorkspaceIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-wstest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var loader = new YamlRulePackLoader();
        _registry = new BuiltinPackRegistry(loader);
        _workspaceOrchestrator = new WorkspaceOrchestrator();
        _generateOrchestrator = new GenerateOrchestrator(
            new LayerInferenceEngine(),
            new CodeGeneratorService());
        _linter = new Linter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void ScaffoldThenGenerate_CleanArchitecture_ShouldProduceValidStructure()
    {
        var pack = _registry.GetPack("clean-architecture").Value;

        // Step 1: Scaffold workspace
        var workspace = _workspaceOrchestrator.GenerateWorkspace(pack, new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders"],
        });

        workspace.Directories.Should().NotBeEmpty();

        // Step 2: Generate components into the workspace
        var generateResult = _generateOrchestrator.Generate(pack, new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = "Entity", Name = "Order" },
                new ComponentRequest { RoleName = "CommandHandler", Name = "CreateOrderHandler" },
            ],
        });

        generateResult.Success.Should().BeTrue();
        generateResult.GeneratedFiles.Should().HaveCount(2);

        // Step 3: Write files to workspace
        foreach (var file in generateResult.GeneratedFiles)
        {
            var fullPath = Path.Combine(_tempDir, file.FilePath);
            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, file.Code);
        }

        Directory.GetFiles(_tempDir, "*.cs", SearchOption.AllDirectories).Should().HaveCount(2);
    }

    [Fact]
    public void ScaffoldThenGenerate_MultipleFeatures_ShouldScaffoldAllFeatureLayers()
    {
        var pack = _registry.GetPack("clean-architecture").Value;

        var workspace = _workspaceOrchestrator.GenerateWorkspace(pack, new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders", "Users"],
        });

        // 2 features × 4 layers = 8
        workspace.Directories.Should().HaveCount(8);

        // Generate a component per feature
        var ordersResult = _generateOrchestrator.Generate(pack, new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components = [new ComponentRequest { RoleName = "Entity", Name = "Order" }],
        });

        var usersResult = _generateOrchestrator.Generate(pack, new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Users",
            Components = [new ComponentRequest { RoleName = "Entity", Name = "User" }],
        });

        ordersResult.Success.Should().BeTrue();
        usersResult.Success.Should().BeTrue();

        // Write both features
        WriteGeneratedFiles(ordersResult);
        WriteGeneratedFiles(usersResult);

        Directory.GetFiles(_tempDir, "*.cs", SearchOption.AllDirectories).Should().HaveCount(2);
    }

    [Fact]
    public void ScaffoldThenLint_GeneratedCode_ShouldPassLint()
    {
        var pack = _registry.GetPack("clean-architecture").Value;

        var generateResult = _generateOrchestrator.Generate(pack, new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = "Entity", Name = "Order" },
                new ComponentRequest { RoleName = "Repository", Name = "OrderRepository" },
                new ComponentRequest { RoleName = "RepositoryInterface", Name = "IOrderRepository" },
            ],
        });

        generateResult.Success.Should().BeTrue();
        WriteGeneratedFiles(generateResult);

        var lintResult = _linter.Lint(pack, _tempDir);

        lintResult.IsClean.Should().BeTrue();
        lintResult.FilesScanned.Should().Be(3);
    }

    [Fact]
    public void ScaffoldThenGenerate_WithComposablePack_ShouldProduceCorrectStructure()
    {
        var merger = new RulePackMerger();
        var basePack = _registry.GetPack("clean-architecture").Value;
        var cqrsPack = _registry.GetPack("cqrs").Value;
        var merged = merger.Merge([basePack, cqrsPack]);

        // Workspace with merged pack should include layers from both packs
        var workspace = _workspaceOrchestrator.GenerateWorkspace(merged, new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders"],
        });

        workspace.Directories.Should().NotBeEmpty();
        workspace.Directories.Should().HaveCount(merged.Layers.Count);

        // Generate CQRS components — should succeed with merged pack
        var generateResult = _generateOrchestrator.Generate(merged, new GenerateRequest
        {
            SolutionName = "MyApp",
            Feature = "Orders",
            Components =
            [
                new ComponentRequest { RoleName = "Command", Name = "CreateOrderCommand" },
                new ComponentRequest { RoleName = "CommandHandler", Name = "CreateOrderCommandHandler" },
                new ComponentRequest { RoleName = "Query", Name = "GetOrderQuery" },
                new ComponentRequest { RoleName = "QueryHandler", Name = "GetOrderQueryHandler" },
            ],
        });

        generateResult.Success.Should().BeTrue();
        generateResult.GeneratedFiles.Should().HaveCount(4);

        // All generated files should have correct namespaces
        generateResult.GeneratedFiles.Should().AllSatisfy(f =>
            f.Namespace.Should().StartWith("MyApp.Orders."));
    }

    [Theory]
    [InlineData("hexagonal-architecture")]
    [InlineData("event-driven")]
    [InlineData("vertical-slice")]
    [InlineData("modular-monolith")]
    [InlineData("onion-architecture")]
    [InlineData("layered")]
    [InlineData("microservice")]
    public void ScaffoldWorkspace_AllTemplates_ShouldProduceConsistentStructure(string templateName)
    {
        var pack = _registry.GetPack(templateName).Value;

        var workspace = _workspaceOrchestrator.GenerateWorkspace(pack, new WorkspaceGenerateRequest
        {
            SolutionName = "TestApp",
            Features = ["Orders"],
        });

        workspace.Directories.Should().HaveCount(pack.Layers.Count);
        workspace.ProjectPaths.Should().HaveCount(pack.Layers.Count);
        workspace.ProjectPaths.Should().AllSatisfy(p => p.Should().EndWith(".csproj"));
    }

    private void WriteGeneratedFiles(GenerateResult result)
    {
        foreach (var file in result.GeneratedFiles)
        {
            var fullPath = Path.Combine(_tempDir, file.FilePath);
            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, file.Code);
        }
    }
}
