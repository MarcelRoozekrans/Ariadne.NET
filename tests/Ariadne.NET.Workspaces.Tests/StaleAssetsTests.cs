using System.Text.Json.Nodes;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>
/// A project whose assets file has no dependency group for its framework keeps unknown packages: a bannedPackages rule that covers
/// the solution validator then reports an error, and a pack without one still validates it. Works on a copy of the restored clean packages fixture.
/// </summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class StaleAssetsTests : IDisposable
{
    // MSBuild itself refuses a truncated file or one without a target for the framework, so such a project never reaches the reader.
    // These two variants still load: the restored graph with no dependency groups at all, and with groups for two other frameworks, which a project without a framework cannot choose between.
    private const string NoGroups = "no groups";
    private const string GroupsForOtherFrameworks = "groups for other frameworks";

    private readonly string _dir = Directory.CreateTempSubdirectory("ariadne-stale-").FullName;
    private readonly SolutionLoader _loader = new();

    public StaleAssetsTests() => FixtureProjects.EnsureRestored();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData(NoGroups)]
    [InlineData(GroupsForOtherFrameworks)]
    public async Task UnusableAssetsFile_LeavesPackagesNull(string variant)
    {
        var solution = CopyWithAssets("Pkg.Core", variant);

        var result = await _loader.LoadAsync(solution);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.First(p => string.Equals(p.Name, "Pkg.Core", StringComparison.Ordinal)).Packages.Should().BeNull();
        loaded.Projects.First(p => string.Equals(p.Name, "Pkg.App", StringComparison.Ordinal)).Packages.Should().NotBeNull();
    }

    [Theory]
    [InlineData(NoGroups)]
    [InlineData(GroupsForOtherFrameworks)]
    public async Task UnusableAssetsFile_BannedPackagesRuleReportsAnError_OtherPackWorks(string variant)
    {
        var solution = CopyWithAssets("Pkg.Core", variant);
        var validator = new SolutionValidator(_loader, new ArchitectureValidator());
        var banning = new RulePack
        {
            Name = "test",
            Rules =
            [
                new RuleDefinition
                {
                    Id = "TEST-BAN",
                    Because = "Test ban.",
                    Kind = RuleKind.BannedPackages,
                    From = new TargetSelector { Assemblies = ["Pkg.Core"] },
                    Packages = ["Anything"],
                },
            ],
        };

        var banned = await validator.ValidateAsync(banning, solution);
        var plain = await validator.ValidateAsync(new RulePack { Name = "plain" }, solution);

        // A stale assets file is something the user fixes by restoring, so it is a reported error and not an exception.
        banned.IsFailure.Should().BeTrue();
        banned.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        banned.Error.Message.Should().Contain("'Pkg.Core'").And.Contain("TEST-BAN").And.Contain("project.assets.json").And.Contain("dotnet restore");
        plain.IsSuccess.Should().BeTrue(plain.IsFailure ? plain.Error.ToString() : null);
        plain.Value.Violations.Should().BeEmpty();
    }

    // The fixtures folder's Directory.Build files come along, so the copy builds the way the original does.
    private string CopyWithAssets(string project, string variant)
    {
        var fixtures = FixtureProjects.Root;
        File.Copy(Path.Combine(fixtures, "Directory.Build.props"), Path.Combine(_dir, "Directory.Build.props"));
        File.Copy(Path.Combine(fixtures, "Directory.Build.targets"), Path.Combine(_dir, "Directory.Build.targets"));
        var target = Path.Combine(_dir, "clean");
        CopyDirectory(Path.Combine(fixtures, "packages", "clean"), target);
        var assetsPath = Path.Combine(target, project, "obj", "project.assets.json");
        var root = JsonNode.Parse(File.ReadAllText(assetsPath))!.AsObject();
        var groups = root["projectFileDependencyGroups"]!.AsObject();
        root["projectFileDependencyGroups"] = string.Equals(variant, NoGroups, StringComparison.Ordinal)
            ? new JsonObject()
            : new JsonObject { ["net9.0"] = groups.First().Value!.DeepClone(), ["net8.0"] = groups.First().Value!.DeepClone() };
        File.WriteAllText(assetsPath, root.ToJsonString());
        return Path.Combine(target, "Packages.slnx");
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
