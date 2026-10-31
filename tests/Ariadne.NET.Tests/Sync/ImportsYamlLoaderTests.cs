using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Tests.Sync;

public class ImportsYamlLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ImportsYamlLoader _loader = new();

    public ImportsYamlLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadFromString_ValidYaml_ShouldReturnManifest()
    {
        var yaml = """
            imports:
              - name: shared-rules
                type: git
                url: https://github.com/org/rules.git
                version: "^1.0.0"
                path: packs/core
              - name: team-rules
                type: git
                url: https://github.com/org/team.git
                version: "2.0.0"
            """;

        var manifest = _loader.LoadFromString(yaml).Value;

        manifest.Imports.Should().HaveCount(2);

        manifest.Imports[0].Name.Should().Be("shared-rules");
        manifest.Imports[0].Type.Should().Be("git");
        manifest.Imports[0].Url.Should().Be("https://github.com/org/rules.git");
        manifest.Imports[0].Version.Should().Be("^1.0.0");
        manifest.Imports[0].Path.Should().Be("packs/core");

        manifest.Imports[1].Name.Should().Be("team-rules");
        manifest.Imports[1].Type.Should().Be("git");
        manifest.Imports[1].Url.Should().Be("https://github.com/org/team.git");
        manifest.Imports[1].Version.Should().Be("2.0.0");
        manifest.Imports[1].Path.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromString_EmptyImports_ShouldReturnEmptyList()
    {
        var yaml = """
            imports: []
            """;

        var manifest = _loader.LoadFromString(yaml).Value;

        manifest.Imports.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromFile_ShouldReadAndParse()
    {
        var yaml = """
            imports:
              - name: shared-rules
                type: git
                url: https://github.com/org/rules.git
                version: "1.0.0"
            """;
        var filePath = Path.Combine(_tempDir, "imports.yaml");
        File.WriteAllText(filePath, yaml);

        var manifest = _loader.LoadFromFile(filePath).Value;

        manifest.Imports.Should().HaveCount(1);
        manifest.Imports[0].Name.Should().Be("shared-rules");
    }

    [Fact]
    public void LoadFromFile_FileNotFound_ReturnsNotFound()
    {
        var missingPath = Path.Combine(_tempDir, "nonexistent.yaml");

        var result = _loader.LoadFromFile(missingPath);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Be($"Imports file '{missingPath}' not found.");
        result.Error.Detail.Should().Be(missingPath);
    }

    [Fact]
    public void LoadFromFile_PathIsDirectory_ReturnsUnavailable()
    {
        var result = _loader.LoadFromFile(_tempDir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
        result.Error.Message.Should().Be($"Imports file '{_tempDir}' could not be read.");
        result.Error.Detail.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoadFromString_MalformedYaml_ReturnsInvalidWithLine()
    {
        var yaml = """
            imports:
              - name: broken
                type: "unterminated
            """;

        var result = _loader.LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().StartWith("Imports YAML could not be parsed: ");
        result.Error.Detail.Should().StartWith("line ");
    }

    [Fact]
    public void LoadFromFile_MalformedYaml_NamesTheFile()
    {
        var filePath = Path.Combine(_tempDir, "imports.yaml");
        File.WriteAllText(filePath, "imports:\n  - name: \"broken\n");

        var result = _loader.LoadFromFile(filePath);

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().StartWith($"Imports file '{filePath}' could not be parsed: ");
    }

    [Fact]
    public void LoadFromString_WrongShape_ReturnsSchemaMismatchWithoutClrTypes()
    {
        var result = _loader.LoadFromString("imports: not-a-list");

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Imports YAML does not match the imports schema.");
        result.Error.Detail.Should().StartWith("line ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("~")]
    public void LoadFromString_Empty_ReturnsInvalid(string yaml)
    {
        var result = _loader.LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Imports YAML is empty.");
    }

    [Theory]
    [InlineData("imports: ~")]
    [InlineData("imports:\n  - ~")]
    public void LoadFromString_NullEntries_ReturnsInvalidWithoutThrowing(string yaml)
    {
        var result = _loader.LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().StartWith("Imports YAML has an empty entry under 'imports'");
    }

    [Fact]
    public void LoadFromString_ExtraFields_ShouldBeIgnored()
    {
        var yaml = """
            imports:
              - name: test-pack
                type: git
                url: https://example.com/pack.git
                version: "1.0.0"
                unknownField: should-be-ignored
            """;

        var manifest = _loader.LoadFromString(yaml).Value;

        manifest.Imports.Should().ContainSingle();
        manifest.Imports[0].Name.Should().Be("test-pack");
    }
}
