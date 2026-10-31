using FluentAssertions;
using Ariadne.NET.Sync;

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
    public void SyncResult_ShouldCaptureOutcome_SuccessDefaultsToTrue()
    {
        var result = new SyncResult
        {
            ImportName = "shared-rules",
            ResolvedVersion = "1.2.3",
            WasCached = true,
            CachePath = "/tmp/cache/shared-rules",
        };

        result.Success.Should().BeTrue();
        result.ImportName.Should().Be("shared-rules");
        result.ResolvedVersion.Should().Be("1.2.3");
        result.WasCached.Should().BeTrue();
        result.CachePath.Should().Be("/tmp/cache/shared-rules");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void SyncResult_WithError_ShouldNotBeSuccess()
    {
        var result = new SyncResult
        {
            ImportName = "broken-rules",
            Success = false,
            Error = "Repository not found",
        };

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Repository not found");
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
