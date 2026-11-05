using System.Runtime.Versioning;
using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;

namespace Ariadne.NET.Tests.RuleLoader;

public class YamlRulePackLoaderTests
{
    private const string V1Hint =
        "'schema: 2' is required; this looks like a v1 pack. See https://marcelroozekrans.github.io/Ariadne.NET/how-it-works/rule-schema#migrating-from-v1";

    [Fact]
    public void LoadFromString_ShouldParseValidYaml()
    {
        var yaml = """
            schema: 2
            name: test-pack
            version: "1.0.0"
            layers:
              - name: Domain
                id: TP-LAYER-DOMAIN
                because: The domain holds the business rules.
                namespaces: ["**.Domain.**"]
                description: "Core domain"
                allowedDependencies: []
              - name: Application
                id: TP-LAYER-APPLICATION
                because: Use cases orchestrate the domain.
                namespaces: ["**.Application.**"]
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
        pack.Schema.Should().Be(2);
        pack.Layers.Should().HaveCount(2);
        pack.Layers[0].Id.Should().Be("TP-LAYER-DOMAIN");
        pack.Layers[0].Because.Should().Be("The domain holds the business rules.");
        pack.Layers[0].Namespaces.Should().Equal("**.Domain.**");
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
    public void V1Pack_FailsWithMigrationHint()
    {
        var yaml = """
            name: legacy
            layers:
              - name: Domain
                allowedDependencies: []
            """;

        var result = new YamlRulePackLoader().LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        result.Error.Detail.Should().Be($"rule pack YAML:1:1: {V1Hint}");
    }

    [Fact]
    public void FileWithTwoProblems_DetailHasPathLineColumnPerLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-two-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, """
            schema: 2
            name: shop
            layers:
              - name: Core
                id: SH-LAYER-CORE
                because: Core holds the business rules.
                namespaces: ["**.Core.**"]
                allowedDependancies: []
            rules:
              - id: SH-001
                forbid:
                  from: { layers: [Core] }
                  to: { namespaces: ["System.Data.**"] }
            """);

        try
        {
            var result = new YamlRulePackLoader().LoadFromFile(path);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be($"Rule pack file '{path}' has 2 problem(s).");
            result.Error.Detail.Should().Be(
                $"{path}:8:5: unknown key 'allowedDependancies' in layer 'Core'; did you mean 'allowedDependencies'?\n" +
                $"{path}:10:5: rule 'SH-001' has no 'because'");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromString_MalformedYaml_ReturnsInvalidWithLine()
    {
        var result = new YamlRulePackLoader().LoadFromString("schema: 2\nlayers: [{name: a}}");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        result.Error.Detail.Should().Be("rule pack YAML:2:19: While parsing a flow sequence, did not find expected ',' or ']'.");
    }

    [Fact]
    public void LoadFromString_UnclosedFlowSequence_ReportsEndOfStream()
    {
        // YamlDotNet marks an unclosed sequence at the end of the stream, one line past the last line.
        var result = new YamlRulePackLoader().LoadFromString("schema: 2\nlayers: [unclosed");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Detail.Should().StartWith("rule pack YAML:3:1: ");
    }

    [Theory]
    [InlineData("- a\n- b", "rule pack YAML:1:1: the pack must be a mapping")]
    [InlineData("just a scalar", "rule pack YAML:1:1: the pack must be a mapping")]
    [InlineData("schema: 2\nname: x\nlayers: \"text\"", "rule pack YAML:3:1: 'layers' in the pack must be a list")]
    public void LoadFromString_WrongShape_ReturnsInvalid(string yaml, string detail)
    {
        var result = new YamlRulePackLoader().LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        result.Error.Detail.Should().Be(detail);
    }

    [Theory]
    [InlineData("", "rule pack YAML:1:1: the pack is empty")]
    [InlineData("   \n  ", "rule pack YAML:1:1: the pack is empty")]
    [InlineData("~", "rule pack YAML:1:1: the pack must be a mapping")]
    public void LoadFromString_Empty_ReturnsInvalid(string yaml, string detail)
    {
        var result = new YamlRulePackLoader().LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        result.Error.Detail.Should().Be(detail);
    }

    [Fact]
    public void LoadFromFile_Empty_NamesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-empty-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "");

        try
        {
            var result = new YamlRulePackLoader().LoadFromFile(path);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be($"Rule pack file '{path}' has 1 problem(s).");
            result.Error.Detail.Should().Be($"{path}:1:1: the pack is empty");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_Malformed_NamesTheFileAndKeepsLocationInDetail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-bad-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "schema: 2\nlayers: [{name: a}}");

        try
        {
            var result = new YamlRulePackLoader().LoadFromFile(path);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be($"Rule pack file '{path}' has 1 problem(s).");
            result.Error.Detail.Should().StartWith($"{path}:2:19: ");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_WrongShape_NamesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-shape-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "- a\n- b");

        try
        {
            var result = new YamlRulePackLoader().LoadFromFile(path);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be($"Rule pack file '{path}' has 1 problem(s).");
            result.Error.Detail.Should().Be($"{path}:1:1: the pack must be a mapping");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [UnixNonRootFact]
    [UnsupportedOSPlatform("windows")]
    public void LoadFromFile_AccessDenied_ReturnsUnavailable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-denied-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "schema: 2\nname: x");
        File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            var result = new YamlRulePackLoader().LoadFromFile(path);

            result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
            result.Error.Message.Should().Be($"Rule pack file '{path}' could not be read.");
            result.Error.Detail.Should().NotBeNullOrEmpty();
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_Missing_ReturnsNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-missing-{Guid.NewGuid():N}", "rules.yaml");

        var result = new YamlRulePackLoader().LoadFromFile(path);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Be($"Rule pack file '{path}' not found.");
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
