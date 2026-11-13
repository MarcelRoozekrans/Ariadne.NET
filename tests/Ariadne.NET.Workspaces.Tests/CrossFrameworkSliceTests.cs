using Ariadne.NET.Analysis;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>
/// Checks a slice cycle whose edges sit in compatible but different target frameworks on the <c>crossframework</c> fixture:
/// Infra (<c>net9.0;net10.0</c>) compiles against the <c>net8.0</c> variant of Billing (<c>netstandard2.0;net8.0</c>).
/// </summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class CrossFrameworkSliceTests
{
    private static readonly SolutionValidator Validator = new(new SolutionLoader(), new ArchitectureValidator());

    public CrossFrameworkSliceTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task Cycle_across_compatible_frameworks_is_reported()
    {
        var report = await ValidateAsync();

        report.SkippedProjects.Should().BeEmpty();
        report.Violations.Should().HaveCount(2).And.OnlyContain(v =>
            v.Kind == ViolationKind.SliceCycle && string.Equals(v.RuleId, "CF-ACYCLIC", StringComparison.Ordinal));
        report.Violations.Select(v => (v.SourceType, v.Target)).Should().BeEquivalentTo(new[]
        {
            ("Cross.Billing.Invoice", "Cross.Orders.Order"),
            ("Cross.Orders.Infrastructure.Repo", "Cross.Billing.Invoice"),
        });
        report.Violations.Should().OnlyContain(v => v.TargetFrameworks.SequenceEqual(new[] { "net10.0", "net9.0" }));
    }

    private static async Task<ArchitectureReport> ValidateAsync()
    {
        var loaded = new YamlRulePackLoader().LoadFromFile(FixtureProjects.Path("crossframework", ".architecture", "rules.yaml"));
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");

        var result = await Validator.ValidateAsync(loaded.Value, FixtureProjects.Path("crossframework", "CrossFramework.slnx")).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
