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

    [Fact]
    public async Task OnlyTheTwoRazorViolations()
    {
        var report = await ValidateAsync();

        report.SkippedProjects.Should().BeEmpty();
        report.Violations.Select(v => (v.RuleId, v.SourceType)).Should().BeEquivalentTo(
        [
            ("RAZOR-NO-INFRASTRUCTURE", "Razor.App.Components.Counter"),
            ("RAZOR-NO-DTO-COPIES", "Razor.App.Components.Counter.ProjectDto"),
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
