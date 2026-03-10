using FluentAssertions;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.Orchestration;

public class WorkspaceOrchestratorTests
{
    private readonly WorkspaceOrchestrator _sut = new();

    private static RulePack CreatePack(string namespacePattern = "{SolutionName}.{Feature}.{Layer}") => new()
    {
        Name = "test",
        Layers =
        [
            new LayerDefinition { Name = "Domain", AllowedDependencies = [] },
            new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] },
            new LayerDefinition { Name = "Infrastructure", AllowedDependencies = ["Domain", "Application"] },
        ],
        NamingConventions = new NamingConventions
        {
            Namespace = namespacePattern,
            FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs",
        },
    };

    [Fact]
    public void GenerateWorkspace_SingleFeature_ShouldCreateProjectPerLayer()
    {
        var pack = CreatePack();
        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders"],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        result.Directories.Should().HaveCount(3);
        result.ProjectPaths.Should().HaveCount(3);
        result.Directories.Should().Contain("src/MyApp.Orders.Domain");
        result.Directories.Should().Contain("src/MyApp.Orders.Application");
        result.Directories.Should().Contain("src/MyApp.Orders.Infrastructure");
    }

    [Fact]
    public void GenerateWorkspace_MultipleFeatures_ShouldCreateProjectsPerFeaturePerLayer()
    {
        var pack = CreatePack();
        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders", "Users", "Products"],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        // 3 features × 3 layers = 9
        result.Directories.Should().HaveCount(9);
        result.ProjectPaths.Should().HaveCount(9);

        result.Directories.Should().Contain("src/MyApp.Orders.Domain");
        result.Directories.Should().Contain("src/MyApp.Users.Application");
        result.Directories.Should().Contain("src/MyApp.Products.Infrastructure");
    }

    [Fact]
    public void GenerateWorkspace_ProjectPaths_ShouldIncludeCsprojFileName()
    {
        var pack = CreatePack();
        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders"],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        result.ProjectPaths.Should().Contain("src/MyApp.Orders.Domain/MyApp.Orders.Domain.csproj");
        result.ProjectPaths.Should().Contain("src/MyApp.Orders.Application/MyApp.Orders.Application.csproj");
        result.ProjectPaths.Should().Contain("src/MyApp.Orders.Infrastructure/MyApp.Orders.Infrastructure.csproj");
    }

    [Fact]
    public void GenerateWorkspace_NullPack_ShouldThrow()
    {
        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders"],
        };

        var act = () => _sut.GenerateWorkspace(null!, request);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateWorkspace_NullRequest_ShouldThrow()
    {
        var pack = CreatePack();

        var act = () => _sut.GenerateWorkspace(pack, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateWorkspace_EmptyFeatures_ShouldReturnNoProjects()
    {
        var pack = CreatePack();
        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = [],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        result.Directories.Should().BeEmpty();
        result.ProjectPaths.Should().BeEmpty();
    }

    [Fact]
    public void GenerateWorkspace_HexagonalArchitecture_ShouldUseCorrectLayers()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var pack = registry.GetPack("hexagonal-architecture");

        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Catalog"],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        result.Directories.Should().HaveCount(pack.Layers.Count);
        result.Directories.Should().OnlyContain(d => d.StartsWith("src/MyApp.Catalog."));
    }

    [Fact]
    public void GenerateWorkspace_EventDriven_ShouldCreateAllLayerProjects()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var pack = registry.GetPack("event-driven");

        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "Eventing",
            Features = ["Notifications"],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        result.Directories.Should().HaveCount(pack.Layers.Count);
        result.ProjectPaths.Should().AllSatisfy(p => p.Should().EndWith(".csproj"));
    }

    [Fact]
    public void GenerateWorkspace_DirectoriesAndProjectPaths_ShouldBeConsistent()
    {
        var pack = CreatePack();
        var request = new WorkspaceGenerateRequest
        {
            SolutionName = "MyApp",
            Features = ["Orders", "Users"],
        };

        var result = _sut.GenerateWorkspace(pack, request);

        // Each directory should have exactly one project path
        result.Directories.Should().HaveCount(result.ProjectPaths.Count);

        foreach (var dir in result.Directories)
        {
            result.ProjectPaths.Should().ContainSingle(p => p.StartsWith(dir + "/"));
        }
    }
}
