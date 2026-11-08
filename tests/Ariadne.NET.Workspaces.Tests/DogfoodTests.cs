using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Checks this repository against its own rule pack, the same run the CI "Validate architecture" step does.</summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class DogfoodTests
{
    [Fact]
    public async Task Dogfood_RepositoryPassesItsOwnPack()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "src", "Ariadne.NET.slnx")))
        {
            root = Path.GetDirectoryName(root);
        }

        root.Should().NotBeNull("the repository root must be reachable from the test output directory");

        var loader = new YamlRulePackLoader();
        var loaded = loader.LoadFromFile(Path.Combine(root!, ".architecture", "rules.yaml"));
        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");
        var resolved = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger()).Resolve(loaded.Value);
        resolved.IsSuccess.Should().BeTrue(resolved.IsFailure ? $"{resolved.Error.Message} {resolved.Error.Detail}" : "");

        var validator = new SolutionValidator(new SolutionLoader(), new ArchitectureValidator());
        var result = await validator.ValidateAsync(resolved.Value, Path.Combine(root!, "src", "Ariadne.NET.slnx"));

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        result.Value.Violations.Should().BeEmpty(
            string.Join(Environment.NewLine, result.Value.Violations.Select(v => $"{v.RuleId} {v.SourceType} -> {v.Target}")));
    }
}
