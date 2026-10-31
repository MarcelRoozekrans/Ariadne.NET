using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;
using FluentAssertions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Sync;

public class ImportDefinitionTests
{
    [Fact]
    public void ImportDefinition_ShouldHaveAllRequiredProperties()
    {
        var import = new ImportDefinition
        {
            Name = "shared-rules",
            Type = "git",
            Url = "https://github.com/org/rules.git",
            Version = "^1.0.0",
        };

        import.Name.Should().Be("shared-rules");
        import.Type.Should().Be("git");
        import.Url.Should().Be("https://github.com/org/rules.git");
        import.Version.Should().Be("^1.0.0");
    }

    [Fact]
    public void ImportDefinition_Path_ShouldDefaultToEmpty()
    {
        var import = new ImportDefinition
        {
            Name = "shared-rules",
            Type = "git",
            Url = "https://github.com/org/rules.git",
            Version = "1.0.0",
        };

        import.Path.Should().BeEmpty();
    }

    [Fact]
    public void ImportsManifest_ShouldHoldListOfImports()
    {
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition
                {
                    Name = "shared-rules",
                    Type = "git",
                    Url = "https://github.com/org/rules.git",
                    Version = "1.0.0",
                },
                new ImportDefinition
                {
                    Name = "team-rules",
                    Type = "git",
                    Url = "https://github.com/org/team.git",
                    Version = "2.0.0",
                    Path = "packs/core",
                },
            ],
        };

        manifest.Imports.Should().HaveCount(2);
        manifest.Imports[1].Path.Should().Be("packs/core");
    }

    [Fact]
    public void SyncedImport_ShouldCaptureOutcome()
    {
        var synced = new SyncedImport("shared-rules", "1.2.3", WasCached: true, "/tmp/cache/shared-rules");

        synced.ImportName.Should().Be("shared-rules");
        synced.ResolvedVersion.Should().Be("1.2.3");
        synced.WasCached.Should().BeTrue();
        synced.CachePath.Should().Be("/tmp/cache/shared-rules");
    }

    [Fact]
    public void ImportSyncOutcome_PairsTheNameWithAFailure()
    {
        var outcome = new ImportSyncOutcome(
            "broken-rules",
            Result<SyncedImport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Unavailable, "Repository not found")));

        outcome.ImportName.Should().Be("broken-rules");
        outcome.Result.IsFailure.Should().BeTrue();
        outcome.Result.Error.Message.Should().Be("Repository not found");
    }

    [Fact]
    public void GitInfo_ShouldCaptureMetadata()
    {
        var syncedAt = DateTimeOffset.UtcNow;

        var info = new GitInfo
        {
            Url = "https://github.com/org/rules.git",
            ResolvedTag = "v1.2.3",
            CommitSha = "abc123def456",
            SyncedAt = syncedAt,
        };

        info.Url.Should().Be("https://github.com/org/rules.git");
        info.ResolvedTag.Should().Be("v1.2.3");
        info.CommitSha.Should().Be("abc123def456");
        info.SyncedAt.Should().Be(syncedAt);
    }
}
