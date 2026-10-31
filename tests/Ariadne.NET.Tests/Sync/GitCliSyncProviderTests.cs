using System.Text.Json;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;
using FluentAssertions;

namespace Ariadne.NET.Tests.Sync;

public class GitCliSyncProviderTests
{
    [Fact]
    public void ParseTagsFromLsRemote_ShouldExtractTagNames()
    {
        var output = string.Join("\n", new[]
        {
            "aaa1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0",
            "ccc1234567890123456789012345678901234567\trefs/tags/v2.0.0",
            "ddd1234567890123456789012345678901234567\trefs/tags/v2.0.0^{}",
            "eee1234567890123456789012345678901234567\trefs/tags/not-semver",
        });

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
        var output = string.Join("\n", new[]
        {
            "aaa1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0",
            "ccc1234567890123456789012345678901234567\trefs/tags/v2.0.0",
        });

        var sha = GitCliSyncProvider.ParseCommitFromLsRemote(output, "v2.0.0");

        sha.Should().Be("ccc1234567890123456789012345678901234567");
    }

    [Fact]
    public void ParseCommitFromLsRemote_DereferencedTag_ShouldPreferDereferenced()
    {
        var output = string.Join("\n", new[]
        {
            "abc1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bcd1234567890123456789012345678901234567\trefs/tags/v1.0.0^{}",
        });

        var sha = GitCliSyncProvider.ParseCommitFromLsRemote(output, "v1.0.0");

        sha.Should().Be("bcd1234567890123456789012345678901234567");
    }

    [Fact]
    public void IsCacheValid_NoCacheDir_ShouldReturnFalse()
    {
        var result = GitCliSyncProvider.IsCacheValid(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            "https://example.com/repo.git",
            SemVerRange.Parse("^1.0").Value);

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
                SemVerRange.Parse("^1.0").Value);

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
                SemVerRange.Parse("^1.0").Value);

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
        var output = string.Join("\n", new[]
        {
            "aaa1234567890123456789012345678901234567\trefs/tags/v1.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0-beta",
            "ccc1234567890123456789012345678901234567\trefs/tags/v2.0.0-rc.1",
            "ddd1234567890123456789012345678901234567\trefs/tags/v2.0.0",
        });

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
                SemVerRange.Parse("^1.0").Value);

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
                SemVerRange.Parse("^2.0").Value);

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
    public async Task SyncAsync_InvalidVersionRange_IsInvalid()
    {
        var import = new ImportDefinition
        {
            Name = "bad-range",
            Type = "git",
            Url = "https://example.com/rules.git",
            Version = "not-valid",
        };

        var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript()))
            .SyncAsync(import, Path.GetTempPath(), noCache: true);

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Invalid semver range: 'not-valid'");
    }

    public static TheoryData<string?, string?, string?, string> IncompleteImports => new()
    {
        { null, "https://example.com/a.git", "^1.0", "An import has no name; give every import in imports.yaml a name." },
        { "", "https://example.com/a.git", "^1.0", "An import has no name; give every import in imports.yaml a name." },
        { "pack", null, "^1.0", "Import 'pack' has no url." },
        { "pack", "", "^1.0", "Import 'pack' has no url." },
        { "pack", "https://example.com/a.git", null, "Import 'pack' has no version." },
        { "pack", "https://example.com/a.git", "", "Import 'pack' has no version." },
        { "..", "https://example.com/a.git", "^1.0", "Import name '..' must not contain path separators or be '.' or '..'." },
        { "../src", "https://example.com/a.git", "^1.0", "Import name '../src' must not contain path separators or be '.' or '..'." },
        { "pack", "--upload-pack=touch x", "^1.0", "Import 'pack' has url '--upload-pack=touch x', which must not start with '-'." },
    };

    [Theory]
    [MemberData(nameof(IncompleteImports))]
    public async Task SyncAsync_IncompleteImport_IsInvalid_AndRunsNoGit(string? name, string? url, string? version, string message)
    {
        var git = new FakeGitScript();
        var import = new ImportDefinition { Name = name!, Type = "git", Url = url!, Version = version! };

        var result = await new GitCliSyncProvider(new FakeGitClient(git)).SyncAsync(import, Path.GetTempPath(), noCache: true);

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be(message);
        git.LsRemoteCalls.Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_NoTagSatisfiesRange_IsNotFound()
    {
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^2.0" };

        var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript()))
            .SyncAsync(import, Path.GetTempPath(), noCache: true);

        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Be("No tag satisfying '^2.0' found in https://example.com/a.git");
    }

    [Fact]
    public async Task SyncAsync_FetchesThenServesFromCache()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"ariadne-provider-{Guid.NewGuid():N}");
        var git = new FakeGitScript();
        var sut = new GitCliSyncProvider(new FakeGitClient(git));
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" };

        try
        {
            var fetched = await sut.SyncAsync(import, cacheDir, noCache: false);
            var cached = await sut.SyncAsync(import, cacheDir, noCache: false);

            fetched.Value.Should().Be(new SyncedImport("pack", "v1.0.0", WasCached: false, Path.Combine(cacheDir, "pack")));
            cached.Value.Should().Be(new SyncedImport("pack", "v1.0.0", WasCached: true, Path.Combine(cacheDir, "pack")));
            File.Exists(Path.Combine(cacheDir, "pack", "pack.yaml")).Should().BeTrue();
            git.LsRemoteCalls.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SyncAsync_CacheCannotBeWritten_IsUnavailable()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"ariadne-provider-{Guid.NewGuid():N}");
        Directory.CreateDirectory(cacheDir);

        // A file where the import's cache directory should go makes creating that directory fail.
        File.WriteAllText(Path.Combine(cacheDir, "pack"), "not a directory");
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript()))
                .SyncAsync(import, cacheDir, noCache: true);

            result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
            result.Error.Message.Should().Be($"Import 'pack' could not be written to the cache at '{Path.Combine(cacheDir, "pack")}'.");
            result.Error.Detail.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Directory.Delete(cacheDir, recursive: true);
        }
    }
}
