using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Issue #127: the code of a Razor component is checked, and its violations point at the <c>.razor</c> file.</summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class RazorComponentTests
{
    public RazorComponentTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task ComponentCode_CrossingALayer_IsReportedAtTheRazorFile()
    {
        var report = await ValidateAsync();

        var violation = report.Violations.Should()
            .ContainSingle(v => string.Equals(v.RuleId, "RAZOR-NO-INFRASTRUCTURE", StringComparison.Ordinal)).Which;
        violation.SourceType.Should().Be("Razor.App.Components.Counter");
        violation.Target.Should().Be("Razor.App.Infrastructure.Clock");
        Path.GetFileName(violation.Location!.Value.FilePath).Should().Be("Counter.razor");
        violation.Location.Value.Line.Should().Be(6);
    }

    [Fact]
    public async Task ComponentRecord_CopyingAContract_IsReported()
    {
        var report = await ValidateAsync();

        var violation = report.Violations.Should()
            .ContainSingle(v => string.Equals(v.RuleId, "RAZOR-NO-DTO-COPIES", StringComparison.Ordinal)).Which;
        violation.SourceType.Should().Be("Razor.App.Components.Counter.ProjectDto");
        Path.GetFileName(violation.Location!.Value.FilePath).Should().Be("Counter.razor");
        violation.Location.Value.Line.Should().Be(8);
    }

    // Hidden code, such as the base type, is reported at line 1 of the component's own file, not at _Imports.razor, whose usings
    // come first in every component tree, and not in the generated file, which the lowercase folder of Banner would sort first.
    [Theory]
    [InlineData("Razor.App.Components.Counter", "Counter.razor")]
    [InlineData("Razor.App.Components.shared.Banner", "Banner.razor")]
    public async Task HiddenComponentCode_IsReportedAtLine1OfItsOwnRazorFile(string component, string file)
    {
        var report = await ValidateAsync();

        var violation = report.Violations.Should().ContainSingle(v =>
            string.Equals(v.RuleId, "RAZOR-PROBE-HIDDEN-CODE", StringComparison.Ordinal)
            && string.Equals(v.SourceType, component, StringComparison.Ordinal)).Which;
        violation.Target.Should().Be("Microsoft.AspNetCore.Components.ComponentBase");
        Path.GetFileName(violation.Location!.Value.FilePath).Should().Be(file);
        violation.Location.Value.Line.Should().Be(1);
        violation.Location.Value.Column.Should().Be(1);
    }

    [Fact]
    public async Task OnlyTheExpectedRazorViolations()
    {
        var report = await ValidateAsync();

        report.SkippedProjects.Should().BeEmpty();
        report.Violations.Select(v => (v.RuleId, v.SourceType)).Should().BeEquivalentTo(
        [
            ("RAZOR-NO-INFRASTRUCTURE", "Razor.App.Components.Counter"),
            ("RAZOR-NO-DTO-COPIES", "Razor.App.Components.Counter.ProjectDto"),
            ("RAZOR-PROBE-HIDDEN-CODE", "Razor.App.Components.Counter"),
            ("RAZOR-PROBE-HIDDEN-CODE", "Razor.App.Components.shared.Banner"),
        ]);
    }

    private static async Task<ArchitectureReport> ValidateAsync()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<IPackSetValidator>()
            .ValidateAsync(FixtureProjects.Path("razor", "Razor.App.csproj")).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? $"{result.Error.Message} {result.Error.Detail}" : null);
        return result.Value;
    }
}
