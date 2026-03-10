using FluentAssertions;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.RuleLoader;

public class YamlRulePackLoaderTests
{
    [Fact]
    public void LoadFromString_ShouldParseValidYaml()
    {
        var yaml = """
            name: test-pack
            version: "1.0.0"
            layers:
              - name: Domain
                description: "Core domain"
                allowedDependencies: []
              - name: Application
                description: "Use cases"
                allowedDependencies:
                  - Domain
            componentRoles:
              - name: Entity
                layer: Domain
                typeKind: Class
                requiresInterface: false
              - name: Repository
                layer: Infrastructure
                typeKind: Class
                requiresInterface: true
                interfaceRole: RepositoryInterface
                accessibility: internal
            namingConventions:
              namespace: "{SolutionName}.{Feature}.{Layer}"
              filePath: "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
            """;

        var loader = new YamlRulePackLoader();
        var pack = loader.LoadFromString(yaml);

        pack.Name.Should().Be("test-pack");
        pack.Layers.Should().HaveCount(2);
        pack.Layers[1].AllowedDependencies.Should().Contain("Domain");
        pack.ComponentRoles.Should().HaveCount(2);
        pack.ComponentRoles[1].RequiresInterface.Should().BeTrue();
        pack.ComponentRoles[1].Accessibility.Should().Be("internal");
    }

    [Fact]
    public void LoadFromFile_ShouldReadAndParseYamlFile()
    {
        var testDataPath = Path.Combine(
            AppContext.BaseDirectory, "RuleLoader", "TestData", "simple-pack.yaml");

        var loader = new YamlRulePackLoader();
        var pack = loader.LoadFromFile(testDataPath);

        pack.Name.Should().Be("simple-test");
        pack.Layers.Should().NotBeEmpty();
    }
}
