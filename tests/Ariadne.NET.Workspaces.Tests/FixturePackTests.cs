using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces.Tests;

[Collection(FixtureSolutionCollection.Name)]
public sealed class FixturePackTests
{
    public FixturePackTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task BrokenFixture_FindsItsOwnPack_AndReportsItsViolation()
    {
        var path = FixtureProjects.Path("broken", "Shop.slnx");

        PackDiscovery.Find(path).Value.ProjectRoot.Should().Be(FixtureProjects.Path("broken"));
        var result = await Validate(path);

        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value.Violations.Should().NotBeEmpty();
        var violation = result.Value.Violations[0];
        violation.Pack.Should().Be("broken-fixture");
        violation.RuleId.Should().Be("SHOP-DOMAIN-NO-INFRASTRUCTURE");
        violation.Because.Should().NotBeNullOrWhiteSpace();
        result.Value.Violations.Should().OnlyContain(v => v.Pack == "broken-fixture");
    }

    [Fact]
    public async Task CleanFixture_FindsItsOwnPack_AndPasses()
    {
        var path = FixtureProjects.Path("clean", "Shop.slnx");

        PackDiscovery.Find(path).Value.ProjectRoot.Should().Be(FixtureProjects.Path("clean"));
        var result = await Validate(path);

        result.IsSuccess.Should().BeTrue(Describe(result));
        result.Value.Violations.Should().BeEmpty();
        result.Value.IsValid.Should().BeTrue();
        result.Value.TypesChecked.Should().BeGreaterThan(0);
    }

    private static async Task<Result<ArchitectureReport, AriadneError>> Validate(string path)
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<IPackSetValidator>().ValidateAsync(path).ConfigureAwait(false);
    }

    private static string Describe<T>(Result<T, AriadneError> result) =>
        result.IsFailure ? $"{result.Error.Message} {result.Error.Detail}" : string.Empty;
}
