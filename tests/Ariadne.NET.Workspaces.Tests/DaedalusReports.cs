using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Runs the Daedalus pack over one variant of the Daedalus fixture solution.</summary>
internal static class DaedalusReports
{
    private static readonly SolutionValidator Validator = new(new SolutionLoader(), new ArchitectureValidator());

    internal static async Task<ArchitectureReport> ValidateAsync(string variant)
    {
        var loader = new YamlRulePackLoader();
        var rulesPath = FixtureProjects.Path("daedalus", "daedalus.rules.yaml");
        var loaded = loader.LoadFromFile(rulesPath);
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");

        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
        var resolved = resolver.Resolve(loaded.Value);
        resolved.IsSuccess.Should().BeTrue(resolved.IsFailure ? $"{resolved.Error.Message} {resolved.Error.Detail}" : "");

        var result = await Validator.ValidateAsync(resolved.Value, FixtureProjects.Path("daedalus", variant, "Daedalus.slnx")).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
