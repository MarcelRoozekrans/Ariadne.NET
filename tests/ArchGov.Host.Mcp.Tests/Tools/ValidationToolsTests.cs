using FluentAssertions;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.Validation;
using ArchGov.Host.Mcp.Tools;

namespace ArchGov.Host.Mcp.Tests.Tools;

public class ValidationToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly SourceValidator _validator;
    private readonly Linter _linter;

    public ValidationToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
        _validator = new SourceValidator();
        _linter = new Linter();
    }

    [Fact]
    public void ValidateSource_EmptyDir_ShouldReturnValid()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = ValidationTools.ValidateSource(
                _helper, _validator,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsValid.Should().BeTrue();
            result.FilesScanned.Should().Be(0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LintSource_EmptyDir_ShouldReturnClean()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = ValidationTools.LintSource(
                _helper, _linter,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsClean.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
