using System.Text.Json;
using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Reads hand-written assets files, to pin the graph walk and every case in which the packages are unknown.</summary>
public sealed class ResolvedPackageReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ariadne-assets-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ProjectChainOfDepthTwo_PathNamesBothProjects()
    {
        var packages = Read(
            Assets("""
                "net10.0": {
                  "Mid/1.0.0": { "type": "project", "dependencies": { "Leaf": "1.0.0" } },
                  "Leaf/1.0.0": { "type": "project", "dependencies": { "Banned": "2.0.0" } },
                  "Banned/2.0.0": { "type": "package" }
                }
                """, """ "net10.0": [ "Mid >= 1.0.0" ] """),
            "net10.0");

        var banned = packages.Should().ContainSingle().Subject;
        banned.IsDirect.Should().BeFalse();
        banned.Path.Should().Equal("Mid", "Leaf", "Banned");
    }

    [Fact]
    public void DependencyCycle_TerminatesWithShortestPaths()
    {
        var packages = Read(
            Assets("""
                "net10.0": {
                  "A/1.0.0": { "type": "package", "dependencies": { "B": "1.0.0" } },
                  "B/1.0.0": { "type": "package", "dependencies": { "A": "1.0.0" } }
                }
                """, """ "net10.0": [ "A >= 1.0.0" ] """),
            "net10.0");

        packages.Select(p => string.Join('>', p.Path)).Should().Equal("A", "A>B");
    }

    [Fact]
    public void UnreachablePackage_IsListedAsTransitiveWithItsOwnPath()
    {
        var packages = Read(
            Assets("""
                "net10.0": {
                  "Direct/1.0.0": { "type": "package" },
                  "Orphan/1.0.0": { "type": "package" }
                }
                """, """ "net10.0": [ "Direct >= 1.0.0" ] """),
            "net10.0");

        var orphan = packages.Single(p => string.Equals(p.Id, "Orphan", StringComparison.Ordinal));
        orphan.IsDirect.Should().BeFalse();
        orphan.Path.Should().Equal("Orphan");
    }

    [Fact]
    public void RuntimeIdentifierKeyAndPlatformSuffix_MatchTheFramework()
    {
        var rid = Read(
            Assets(""" "net10.0/linux-x64": { "P/1.0.0": { "type": "package" } } """, """ "net10.0": [ "P >= 1.0.0" ] """),
            "net10.0");
        var platform = Read(
            Assets(""" "net10.0-windows7.0": { "Q/1.0.0": { "type": "package" } } """, """ "net10.0-windows7.0": [ "Q >= 1.0.0" ] """),
            "net10.0-windows");

        rid.Select(p => p.Id).Should().Equal("P");
        platform.Select(p => p.Id).Should().Equal("Q");
    }

    [Fact]
    public void FrameworkThatIsAPrefixOfAnotherFramework_DoesNotMatchIt()
    {
        var known = ResolvedPackageReader.TryRead(
            Write(Assets(""" "net48": { "P/1.0.0": { "type": "package" } } """, """ "net48": [ "P >= 1.0.0" ] """)), "net4", out _);

        known.Should().BeFalse();
    }

    [Fact]
    public void NoFramework_UsesTheOnlyEntry()
    {
        var packages = Read(Assets(""" "net10.0": { "P/1.0.0": { "type": "package" } } """, """ "net10.0": [ "P >= 1.0.0" ] """), null);

        packages.Select(p => p.IsDirect).Should().Equal(true);
    }

    [Fact]
    public void NoFramework_RuntimeIdentifierEntriesOfOneFramework_AreOneFramework()
    {
        var targets = """ "net10.0": { "P/1.0.0": { "type": "package" } }, "net10.0/linux-x64": { "P/1.0.0": { "type": "package" }, "Q/1.0.0": { "type": "package" } } """;

        var packages = Read(Assets(targets, """ "net10.0": [ "P >= 1.0.0" ] """), null);

        packages.Select(p => p.Id).Should().Equal("P");
    }

    [Fact]
    public void NoFramework_TwoFrameworks_AreUnknown()
    {
        var targets = """ "net9.0": { "P/1.0.0": { "type": "package" } }, "net10.0": { "P/1.0.0": { "type": "package" } } """;
        var path = Write(Assets(targets, """ "net9.0": [ "P >= 1.0.0" ], "net10.0": [ "P >= 1.0.0" ] """));

        ResolvedPackageReader.TryRead(path, null, out _).Should().BeFalse();
    }

    [Fact]
    public void MalformedJson_Throws()
    {
        var path = Write("""{ "targets": { "net10.0": """);

        var read = () => ResolvedPackageReader.TryRead(path, "net10.0", out _);

        read.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("""{ "projectFileDependencyGroups": { "net10.0": [] } }""", "net10.0")]
    [InlineData("""[ 1, 2 ]""", "net10.0")]
    [InlineData("""{ "targets": [ 1 ], "projectFileDependencyGroups": { "net10.0": [] } }""", "net10.0")]
    [InlineData("""{ "targets": { "net10.0": {} }, "projectFileDependencyGroups": [] }""", "net10.0")]
    [InlineData("""{ "targets": { "net10.0": {}, "net9.0": {} }, "projectFileDependencyGroups": { "net10.0": [], "net9.0": [] } }""", "net8.0")]
    [InlineData("""{ "targets": { "net10.0": {}, "net9.0": {} }, "projectFileDependencyGroups": { "net10.0": [], "net9.0": [] } }""", null)]
    [InlineData("""{ "targets": { "net10.0": {} }, "projectFileDependencyGroups": { "net10.0": [] } }""", "net9.0")]
    [InlineData("""{ "targets": { "net10.0": {} } }""", "net10.0")]
    [InlineData("""{ "targets": { "net10.0": {} }, "projectFileDependencyGroups": { "net9.0": [] } }""", "net10.0")]
    [InlineData("""{ "targets": { "net10.0": [ 1 ] }, "projectFileDependencyGroups": { "net10.0": [] } }""", "net10.0")]
    [InlineData("""{ "targets": { "net10.0": "x" }, "projectFileDependencyGroups": { "net10.0": [] } }""", null)]
    public void MissingOrUnmatchedEntry_IsUnknownNotEmpty(string json, string? framework)
    {
        var known = ResolvedPackageReader.TryRead(Write(json), framework, out var packages);

        known.Should().BeFalse();
        packages.Should().BeEmpty();
    }

    [Fact]
    public void TargetEntryThatIsNotAnObject_IsSkipped()
    {
        var packages = Read(
            Assets("""
                "net10.0": { "Odd/1.0.0": 5, "P/1.0.0": { "type": "package" } }
                """, """ "net10.0": [ "P >= 1.0.0" ] """),
            "net10.0");

        packages.Select(p => p.Id).Should().Equal("P");
    }

    private static string Assets(string targets, string groups) =>
        $$"""{ "version": 3, "targets": { {{targets}} }, "projectFileDependencyGroups": { {{groups}} } }""";

    private string Write(string json)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private IReadOnlyList<ResolvedPackage> Read(string json, string? framework)
    {
        ResolvedPackageReader.TryRead(Write(json), framework, out var packages).Should().BeTrue();
        return packages;
    }
}
