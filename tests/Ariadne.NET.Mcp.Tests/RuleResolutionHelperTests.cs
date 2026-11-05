using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Mcp.Tests;

[Collection(WorkingDirectoryCollection.Name)]
public sealed class RuleResolutionHelperTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly string _tempDir;
    private readonly RuleResolutionHelper _helper;

    public RuleResolutionHelperTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-mcp-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, ".architecture"));
        Directory.SetCurrentDirectory(_tempDir);

        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Resolve_RulesFileExtendsImport_LoadsFromCacheInWorkingDirectory()
    {
        var cacheDir = Path.Combine(_tempDir, ".architecture", ".cache", "git", "x");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "x.yaml"), """
            schema: 2
            name: x
            layers:
              - name: Kernel
                id: X-LAYER-KERNEL
                because: The kernel depends on nothing else.
                namespaces: ["**.Kernel.**"]
                allowedDependencies: []
            """);
        WriteRules("""
            schema: 2
            name: user-rules
            extends:
              - import://x
            """);

        var result = _helper.Resolve(template: null);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.Layers.Should().ContainSingle(l => l.Name == "Kernel");
    }

    [Fact]
    public void Resolve_MalformedRulesFile_ReturnsInvalid()
    {
        WriteRules("schema: 2\nname: x\nlayers: [unclosed");

        var result = _helper.Resolve(template: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
    }

    [Fact]
    public void Resolve_NoRulesFile_FallsBackToCleanArchitecture()
    {
        var result = _helper.Resolve(template: null);

        result.Value.Layers.Should().Contain(l => l.Name == "Domain");
    }

    [Fact]
    public void Resolve_UnknownTemplate_ReturnsNotFound()
    {
        var result = _helper.Resolve("nope");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
    }

    [Fact]
    public void Resolve_TemplateWithoutNames_ReturnsInvalid()
    {
        var result = _helper.Resolve(" + ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
    }

    private void WriteRules(string content) =>
        File.WriteAllText(Path.Combine(_tempDir, ".architecture", "rules.yaml"), content);
}
