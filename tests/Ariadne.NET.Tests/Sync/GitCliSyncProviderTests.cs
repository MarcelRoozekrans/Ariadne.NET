using System.Text.Json;
using FluentAssertions;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Tests.Sync;

public class GitCliSyncProviderTests
{
    [Fact]
    public void ParseTagsFromLsRemote_ShouldExtractTagNames()
    {
        var output = string.Join('\n',
            "aaa1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0",
            "ccc1234567890123456789012345678901234567\trefs/tags/v2.0.0",
            "ddd1234567890123456789012345678901234567\trefs/tags/v2.0.0^{}",
            "eee1234567890123456789012345678901234567\trefs/tags/not-semver");

        var tags = GitCliSyncProvider.ParseTagsFromLsRemote(output);

        tags.Should().BeEquivalentTo(["v1.0.0", "v1.1.0", "v2.0.0"]);
    }

    [Fact]
    public void ParseTagsFromLsRemote_EmptyOutput_ShouldReturnEmptyList()
    {
        var tags = GitCliSyncProvider.ParseTagsFromLsRemote("");

        tags.Should().BeEmpty();
    }

    [Fact]
    public void ParseCommitFromLsRemote_ShouldExtractShaForTag()
    {
        var output = string.Join('\n',
            "aaa1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0",
            "ccc1234567890123456789012345678901234567\trefs/tags/v2.0.0");

        var sha = GitCliSyncProvider.ParseCommitFromLsRemote(output, "v2.0.0");

        sha.Should().Be("ccc1234567890123456789012345678901234567");
    }

    [Fact]
    public void ParseCommitFromLsRemote_DereferencedTag_ShouldPreferDereferenced()
    {
        var output = string.Join('\n',
            "abc1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bcd1234567890123456789012345678901234567\trefs/tags/v1.0.0^{}");

        var sha = GitCliSyncProvider.ParseCommitFromLsRemote(output, "v1.0.0");

        sha.Should().Be("bcd1234567890123456789012345678901234567");
    }

    [Fact]
    public void IsCacheValid_NoCacheDir_ShouldReturnFalse()
    {
        var result = GitCliSyncProvider.IsCacheValid(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            "https://example.com/repo.git",
            SemVerRange.Parse("^1.0"));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsCacheValid_ValidCache_ShouldReturnTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var gitInfo = new GitInfo
            {
                Url = "https://example.com/repo.git",
                ResolvedTag = "v1.2.0",
                CommitSha = "abc123",
                SyncedAt = DateTimeOffset.UtcNow
            };
            File.WriteAllText(
                Path.Combine(tempDir, ".git-info.json"),
                JsonSerializer.Serialize(gitInfo));

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://example.com/repo.git",
                SemVerRange.Parse("^1.0"));

            result.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void IsCacheValid_DifferentUrl_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var gitInfo = new GitInfo
            {
                Url = "https://example.com/other-repo.git",
                ResolvedTag = "v1.2.0",
                CommitSha = "abc123",
                SyncedAt = DateTimeOffset.UtcNow
            };
            File.WriteAllText(
                Path.Combine(tempDir, ".git-info.json"),
                JsonSerializer.Serialize(gitInfo));

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://example.com/repo.git",
                SemVerRange.Parse("^1.0"));

            result.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ParseTagsFromLsRemote_ShouldIgnorePreReleaseTags()
    {
        var output = string.Join('\n',
            "aaa1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0-beta",
            "ccc1234567890123456789012345678901234567\trefs/tags/v2.0.0-rc.1",
            "ddd1234567890123456789012345678901234567\trefs/tags/v2.0.0");

        var tags = GitCliSyncProvider.ParseTagsFromLsRemote(output);

        tags.Should().BeEquivalentTo(["v1.0.0", "v2.0.0"]);
    }

    [Fact]
    public void IsCacheValid_CorruptedGitInfo_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(
                Path.Combine(tempDir, ".git-info.json"),
                "{ this is not valid json }");

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://example.com/repo.git",
                SemVerRange.Parse("^1.0"));

            result.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void IsCacheValid_VersionNotSatisfied_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            var gitInfo = new GitInfo
            {
                Url = "https://example.com/repo.git",
                ResolvedTag = "v1.2.0",
                CommitSha = "abc123",
                SyncedAt = DateTimeOffset.UtcNow
            };
            File.WriteAllText(
                Path.Combine(tempDir, ".git-info.json"),
                JsonSerializer.Serialize(gitInfo));

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://example.com/repo.git",
                SemVerRange.Parse("^2.0"));

            result.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
