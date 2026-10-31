using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;

namespace Ariadne.NET.Tests.RuleLoader;

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
        var pack = loader.LoadFromString(yaml).Value;

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
        var pack = loader.LoadFromFile(testDataPath).Value;

        pack.Name.Should().Be("simple-test");
        pack.Layers.Should().NotBeEmpty();
    }

    [Fact]
    public void LoadFromString_MalformedYaml_ReturnsInvalidWithLine()
    {
        var result = new YamlRulePackLoader().LoadFromString("name: x\nlayers: [{name: a}}");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("did not find expected ',' or ']'");
        result.Error.Detail.Should().Be("line 2, column 19");
    }

    [Fact]
    public void LoadFromString_UnclosedFlowSequence_ReportsEndOfStream()
    {
        // YamlDotNet marks an unclosed sequence at the end of the stream, one line past the last line.
        var result = new YamlRulePackLoader().LoadFromString("name: x\nlayers: [unclosed");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Detail.Should().Be("line 3, column 1");
    }

    [Theory]
    [InlineData("- a\n- b")]
    [InlineData("just a scalar")]
    public void LoadFromString_WrongShape_ReturnsInvalid(string yaml)
    {
        var result = new YamlRulePackLoader().LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Detail.Should().StartWith("line ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("~")]
    public void LoadFromString_Empty_ReturnsInvalid(string yaml)
    {
        var result = new YamlRulePackLoader().LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("rules file is empty");
    }

    [Fact]
    public void LoadFromFile_Missing_ReturnsNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-missing-{Guid.NewGuid():N}", "rules.yaml");

        var result = new YamlRulePackLoader().LoadFromFile(path);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Detail.Should().Be(path);
    }

    [Fact]
    public void LoadFromFile_PathIsDirectory_ReturnsUnavailable()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ariadne-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var result = new YamlRulePackLoader().LoadFromFile(dir);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
            result.Error.Detail.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
