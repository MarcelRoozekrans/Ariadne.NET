using Ariadne.NET.Analysis;
using Ariadne.NET.Tests.Shared;
using Ariadne.NET.Workspaces;
using FluentAssertions;
using ModelContextProtocol;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Validation;
using Ariadne.NET.Mcp.Tools;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class ValidationToolsTests
{
    private readonly RuleResolutionHelper _helper;
    private readonly SolutionValidator _validator;
    private readonly Linter _linter;

    public ValidationToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
        _validator = new SolutionValidator(new SolutionLoader(), new ArchitectureValidator());
        _linter = new Linter();
    }

    [Fact]
    public async Task ValidateSource_CleanFixture_IsValid()
    {
        FixtureProjects.EnsureRestored();

        var report = await ValidationTools.ValidateSource(
            _helper, _validator,
            path: FixtureProjects.Path("clean", "Shop.slnx"),
            template: "clean-architecture");

        report.IsValid.Should().BeTrue();
        report.TypesChecked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateSource_BrokenFixture_HasOneViolation()
    {
        FixtureProjects.EnsureRestored();

        var report = await ValidationTools.ValidateSource(
            _helper, _validator,
            path: FixtureProjects.Path("broken", "Shop.slnx"),
            template: "clean-architecture");

        report.IsValid.Should().BeFalse();
        report.Violations.Should().ContainSingle(v => v.Kind == ViolationKind.DependencyNotAllowed);
    }

    [Fact]
    public async Task ValidateSource_MissingPath_IsToolError()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ariadne-missing-{Guid.NewGuid():N}", "Nope.slnx");

        var act = () => ValidationTools.ValidateSource(
            _helper, _validator,
            path: missing,
            template: "clean-architecture");

        await act.Should().ThrowAsync<McpException>();
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

    [Fact]
    public void LintSource_NamespaceMismatch_ShouldReturnViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var domainDir = Path.Combine(tempDir, "MyApp.Orders.Domain");
            Directory.CreateDirectory(domainDir);
            File.WriteAllText(Path.Combine(domainDir, "Misplaced.cs"), """
                namespace MyApp.Orders.Infrastructure;

                public class Misplaced { }
                """);

            var result = ValidationTools.LintSource(
                _helper, _linter,
                sourceDirectory: tempDir,
                template: "clean-architecture");

            result.IsClean.Should().BeFalse();
            result.Violations.Should().ContainSingle(v =>
                v.Category == "NamespaceMismatch");
            result.FilesScanned.Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

}
