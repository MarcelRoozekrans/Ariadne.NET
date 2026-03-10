using FluentAssertions;
using ArchGov.Core.Sync;

namespace ArchGov.Core.Tests.Sync;

public class ImportsYamlLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ImportsYamlLoader _loader = new();

    public ImportsYamlLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"archgov-tests-{Guid.NewGuid():N}");
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

        var manifest = _loader.LoadFromString(yaml);

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

        var manifest = _loader.LoadFromString(yaml);

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

        var manifest = _loader.LoadFromFile(filePath);

        manifest.Imports.Should().HaveCount(1);
        manifest.Imports[0].Name.Should().Be("shared-rules");
    }

    [Fact]
    public void LoadFromFile_FileNotFound_ShouldThrow()
    {
        var missingPath = Path.Combine(_tempDir, "nonexistent.yaml");

        var act = () => _loader.LoadFromFile(missingPath);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadFromString_MalformedYaml_ShouldThrow()
    {
        var yaml = """
            imports:
              - name: broken
                type: [this is not valid
            """;

        var act = () => _loader.LoadFromString(yaml);

        act.Should().Throw<Exception>();
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

        var manifest = _loader.LoadFromString(yaml);

        manifest.Imports.Should().ContainSingle();
        manifest.Imports[0].Name.Should().Be("test-pack");
    }
}
