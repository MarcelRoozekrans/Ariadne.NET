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

        var tags = SyncOperations.ParseTagsFromLsRemote(output);

        tags.Should().BeEquivalentTo(["v1.0.0", "v1.1.0", "v2.0.0"]);
    }

    [Fact]
    public void ParseTagsFromLsRemote_EmptyOutput_ShouldReturnEmptyList()
    {
        var tags = SyncOperations.ParseTagsFromLsRemote("");

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

        var sha = SyncOperations.ParseCommitFromLsRemote(output, "v2.0.0");

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

        var sha = SyncOperations.ParseCommitFromLsRemote(output, "v1.0.0");

        sha.Should().Be("bcd1234567890123456789012345678901234567");
    }

    [Fact]
    public void IsCacheValid_NoCacheDir_ShouldReturnFalse()
    {
        var result = SyncOperations.IsCacheValid(
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

            var result = SyncOperations.IsCacheValid(
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

            var result = SyncOperations.IsCacheValid(
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

        var tags = SyncOperations.ParseTagsFromLsRemote(output);

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

            var result = SyncOperations.IsCacheValid(
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

            var result = SyncOperations.IsCacheValid(
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

    private static string BadName(string name) =>
        $"Import name '{name}' must be a plain directory name: no path separators, drive, '.' or '..', or characters such as ':' or '*'.";

    public static TheoryData<string?, string?, string?, string> IncompleteImports => new()
    {
        { null, "https://example.com/a.git", "^1.0", "An import has no name; give every import in imports.yaml a name." },
        { "", "https://example.com/a.git", "^1.0", "An import has no name; give every import in imports.yaml a name." },
        { "pack", null, "^1.0", "Import 'pack' has no url." },
        { "pack", "", "^1.0", "Import 'pack' has no url." },
        { "pack", "https://example.com/a.git", null, "Import 'pack' has no version." },
        { "pack", "https://example.com/a.git", "", "Import 'pack' has no version." },
        { ".", "https://example.com/a.git", "^1.0", BadName(".") },
        { "..", "https://example.com/a.git", "^1.0", BadName("..") },
        { "../src", "https://example.com/a.git", "^1.0", BadName("../src") },
        { "a/../..", "https://example.com/a.git", "^1.0", BadName("a/../..") },
        { "/abs", "https://example.com/a.git", "^1.0", BadName("/abs") },
        { "C:", "https://example.com/a.git", "^1.0", BadName("C:") },
        { "C:foo", "https://example.com/a.git", "^1.0", BadName("C:foo") },
        { "a\\b", "https://example.com/a.git", "^1.0", BadName("a\\b") },
        { "a*b", "https://example.com/a.git", "^1.0", BadName("a*b") },
        { "a\0b", "https://example.com/a.git", "^1.0", BadName("a\0b") },
        { "pack", "--upload-pack=touch x", "^1.0", "Import 'pack' has url '--upload-pack=touch x', which must not start with '-'." },
        { "pack", "-u https://token@example.com/a.git", "^1.0", "Import 'pack' has url '-u https://***@example.com/a.git', which must not start with '-'." },
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

    public static TheoryData<string> EscapingPaths => new() { "/etc", "..", "../..", "packs/../../x", "../pack" };

    [Theory]
    [MemberData(nameof(EscapingPaths))]
    public async Task SyncAsync_PathOutsideTheRepository_IsInvalid_AndCopiesNothing(string path)
    {
        var cacheDir = NewCacheDir();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0", Path = path };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript())).SyncAsync(import, cacheDir, noCache: true);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be($"Import 'pack' has path '{path}', which must be a relative folder inside the repository.");
            Directory.Exists(Path.Combine(cacheDir, "pack")).Should().BeFalse();
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task SyncAsync_PathInsideTheRepository_CopiesOnlyThatFolder()
    {
        var cacheDir = NewCacheDir();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0", Path = "packs" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript())).SyncAsync(import, cacheDir, noCache: true);

            result.IsSuccess.Should().BeTrue();
            File.Exists(Path.Combine(cacheDir, "pack", "core.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(cacheDir, "pack", "pack.yaml")).Should().BeFalse();
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Theory]
    [InlineData("/srv/rules repo --template /tmp/evil")]
    [InlineData("/srv/rules \"quoted\" repo")]
    public async Task SyncAsync_UrlWithSpaceOrQuote_ReachesGitAsOneArgument(string url)
    {
        var cacheDir = NewCacheDir();
        var git = new FakeGitScript();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = url, Version = "^1.0" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(git)).SyncAsync(import, cacheDir, noCache: true);

            result.IsSuccess.Should().BeTrue();
            git.Invocations.Should().HaveCount(2);
            git.Invocations[0].Should().Equal("ls-remote", "--tags", "--", url);
            git.Invocations[1].Take(8).Should().Equal("clone", "--depth", "1", "--branch", "v1.0.0", "--single-branch", "--", url);
            git.Invocations[1].Should().HaveCount(9, "the clone target is the only argument after the url");
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task SyncAsync_CloneFails_KeepsTheExistingCache_AndLeavesNoStaging()
    {
        var cacheDir = NewCacheDir();
        var git = new FakeGitScript();
        var sut = new GitCliSyncProvider(new FakeGitClient(git));
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" };

        try
        {
            (await sut.SyncAsync(import, cacheDir, noCache: false)).IsSuccess.Should().BeTrue();
            git.CloneFails = true;

            var result = await sut.SyncAsync(import, cacheDir, noCache: true);

            result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
            result.Error.Detail.Should().Be("fatal: clone failed");
            File.Exists(Path.Combine(cacheDir, "pack", "pack.yaml")).Should().BeTrue();
            File.Exists(Path.Combine(cacheDir, "pack", ".git-info.json")).Should().BeTrue();
            Directory.GetFileSystemEntries(cacheDir).Should().Equal(Path.Combine(cacheDir, "pack"));
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task SyncAsync_UrlWithCredentials_IsRedactedInMessages()
    {
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://user:s3cret@example.com/a.git", Version = "^2.0" };

        var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript()))
            .SyncAsync(import, Path.GetTempPath(), noCache: true);

        result.Error.Message.Should().Be("No tag satisfying '^2.0' found in https://***@example.com/a.git");
    }

    [Theory]
    [InlineData("https://token@host/x", "https://***@host/x")]
    [InlineData("git ls-remote --tags -- https://u:p@host/x failed: fatal: 'https://u:p@host/x' not found", "git ls-remote --tags -- https://***@host/x failed: fatal: 'https://***@host/x' not found")]
    [InlineData("https://user:p@ss@host/x", "https://***@host/x")]
    [InlineData("https://user:p@ss@host?x=a@b", "https://***@host?x=a@b")]
    [InlineData("https://host/x", "https://host/x")]
    [InlineData("git@github.com:org/repo.git", "git@github.com:org/repo.git")]
    [InlineData("ssh://git@host/x", "ssh://***@host/x")]
    public void UrlRedaction_HidesUserInfo(string text, string expected)
    {
        UrlRedaction.Redact(text).Should().Be(expected);
    }

    [UnixFact]
    public async Task SyncAsync_SymlinkedFileOutsideTheClone_IsInvalid_AndCopiesNothing()
    {
        var root = NewCacheDir();
        var outside = Path.Combine(root, "outside.yaml");
        Directory.CreateDirectory(root);
        File.WriteAllText(outside, "secret: true\n");
        var git = new FakeGitScript { OnClone = target => File.CreateSymbolicLink(Path.Combine(target, "secret.yaml"), outside) };

        await AssertLinkIsRefusedAsync(git, root, path: "", expectedLink: "secret.yaml");
    }

    [UnixFact]
    public async Task SyncAsync_SymlinkedDirectoryOutsideTheClone_IsInvalid_AndCopiesNothing()
    {
        var root = NewCacheDir();
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.yaml"), "secret: true\n");
        var git = new FakeGitScript
        {
            OnClone = target => Directory.CreateSymbolicLink(Path.Combine(target, "packs", "linked"), outside),
        };

        await AssertLinkIsRefusedAsync(git, root, path: "", expectedLink: Path.Combine("packs", "linked"));
    }

    [UnixFact]
    public async Task SyncAsync_ImportPathThroughASymlink_IsInvalid_AndCopiesNothing()
    {
        var root = NewCacheDir();
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.yaml"), "secret: true\n");
        var git = new FakeGitScript { OnClone = target => Directory.CreateSymbolicLink(Path.Combine(target, "rules"), outside) };

        await AssertLinkIsRefusedAsync(git, root, path: "rules", expectedLink: "rules");
    }

    private static async Task AssertLinkIsRefusedAsync(FakeGitScript git, string root, string path, string expectedLink)
    {
        var cacheDir = Path.Combine(root, "cache");
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0", Path = path };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(git)).SyncAsync(import, cacheDir, noCache: true).ConfigureAwait(false);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be(
                $"Import 'pack' contains a symbolic link at '{expectedLink}'; pack files must be regular files inside the repository.");
            Directory.Exists(Path.Combine(cacheDir, "pack")).Should().BeFalse();
            Directory.GetFileSystemEntries(cacheDir).Should().BeEmpty("the staging directory is removed");
        }
        finally
        {
            DeleteDir(root);
        }
    }

    [Fact]
    public async Task SyncAsync_PathMissingFromTheRepository_IsNotFound()
    {
        var cacheDir = NewCacheDir();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0", Path = "nope" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript())).SyncAsync(import, cacheDir, noCache: true);

            result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
            result.Error.Message.Should().Be("Import 'pack' has path 'nope', which does not exist in the repository at v1.0.0.");
            Directory.GetFileSystemEntries(cacheDir).Should().BeEmpty();
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".GIT")]
    [InlineData(".git/hooks")]
    [InlineData("./.git")]
    [InlineData("packs/../.Git")]
    [InlineData("GIT~1")]
    [InlineData("git~12/hooks")]
    public async Task SyncAsync_PathIntoTheGitFolder_IsInvalid(string path)
    {
        var git = new FakeGitScript();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0", Path = path };

        var result = await new GitCliSyncProvider(new FakeGitClient(git)).SyncAsync(import, NewCacheDir(), noCache: true);

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be($"Import 'pack' has path '{path}', which points into the repository's .git folder.");
        git.CloneCalls.Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_UrlWithCredentials_IsStoredRedacted_AndStillServesTheCache()
    {
        var cacheDir = NewCacheDir();
        var sut = new GitCliSyncProvider(new FakeGitClient(new FakeGitScript()));
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://user:s3cret@example.com/a.git", Version = "^1.0" };

        try
        {
            (await sut.SyncAsync(import, cacheDir, noCache: false)).IsSuccess.Should().BeTrue();
            var cached = await sut.SyncAsync(import, cacheDir, noCache: false);

            var record = await File.ReadAllTextAsync(Path.Combine(cacheDir, "pack", ".git-info.json"));
            record.Should().NotContain("s3cret").And.Contain("https://***@example.com/a.git");
            cached.Value.WasCached.Should().BeTrue();
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Fact]
    public void IsCacheValid_RecordWithRawCredentials_StillMatches()
    {
        var cacheDir = NewCacheDir();
        Directory.CreateDirectory(cacheDir);
        var gitInfo = new GitInfo
        {
            Url = "https://user:s3cret@example.com/a.git",
            ResolvedTag = "v1.0.0",
            CommitSha = "abc",
            SyncedAt = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(Path.Combine(cacheDir, ".git-info.json"), JsonSerializer.Serialize(gitInfo));

        try
        {
            SyncOperations.IsCacheValid(cacheDir, "https://user:s3cret@example.com/a.git", SemVerRange.Parse("^1.0").Value)
                .Should().BeTrue();
            SyncOperations.IsCacheValid(cacheDir, "https://example.com/other.git", SemVerRange.Parse("^1.0").Value)
                .Should().BeFalse();
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task SyncAsync_CacheHitOnLegacyRecordWithCredentials_RewritesItRedacted()
    {
        var cacheDir = NewCacheDir();
        var syncedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var recordPath = WriteLegacyCache(cacheDir, syncedAt);
        var git = new FakeGitScript();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://user:s3cret@example.com/a.git", Version = "^1.0" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(git)).SyncAsync(import, cacheDir, noCache: false);

            result.Value.WasCached.Should().BeTrue();
            git.Invocations.Should().BeEmpty();
            var record = JsonSerializer.Deserialize<GitInfo>(await File.ReadAllTextAsync(recordPath))!;
            record.Url.Should().Be("https://***@example.com/a.git");
            record.ResolvedTag.Should().Be("v1.0.0");
            record.CommitSha.Should().Be("abc");
            record.SyncedAt.Should().Be(syncedAt);
        }
        finally
        {
            DeleteDir(cacheDir);
        }
    }

    [UnixNonRootFact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task SyncAsync_LegacyRecordCannotBeRewritten_StillServesTheCache()
    {
        var cacheDir = NewCacheDir();
        var recordPath = WriteLegacyCache(cacheDir, DateTimeOffset.UtcNow);
        File.SetUnixFileMode(recordPath, UnixFileMode.UserRead);
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://user:s3cret@example.com/a.git", Version = "^1.0" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(new FakeGitScript())).SyncAsync(import, cacheDir, noCache: false);

            result.Value.WasCached.Should().BeTrue();
            (await File.ReadAllTextAsync(recordPath)).Should().Contain("s3cret", "the read-only record could not be rewritten");
        }
        finally
        {
            File.SetUnixFileMode(recordPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            DeleteDir(cacheDir);
        }
    }

    /// <summary>Writes a cache entry for 'pack' whose record still holds the raw url, as before urls were redacted.</summary>
    private static string WriteLegacyCache(string cacheDir, DateTimeOffset syncedAt)
    {
        var packDir = Path.Combine(cacheDir, "pack");
        Directory.CreateDirectory(packDir);
        File.WriteAllText(Path.Combine(packDir, "pack.yaml"), "name: pack\n");
        var recordPath = Path.Combine(packDir, ".git-info.json");
        File.WriteAllText(recordPath, JsonSerializer.Serialize(new GitInfo
        {
            Url = "https://user:s3cret@example.com/a.git",
            ResolvedTag = "v1.0.0",
            CommitSha = "abc",
            SyncedAt = syncedAt,
        }));
        return recordPath;
    }

    private static string NewCacheDir() => Path.Combine(Path.GetTempPath(), $"ariadne-provider-{Guid.NewGuid():N}");

    private static void DeleteDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ParseTagsFromLsRemote_OverflowingTag_IsIgnored()
    {
        var output = string.Join("\n", new[]
        {
            "aaa1234567890123456789012345678901234567\trefs/tags/v99999999999.0.0",
            "bbb1234567890123456789012345678901234567\trefs/tags/v1.1.0",
        });

        SyncOperations.ParseTagsFromLsRemote(output).Should().Equal("v1.1.0");
    }

    [Fact]
    public void IsCacheValid_NullCachedTag_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(
                Path.Combine(tempDir, ".git-info.json"),
                """{ "url": "https://example.com/repo.git", "resolvedTag": null, "commitSha": "abc", "syncedAt": "2026-01-01T00:00:00Z" }""");

            SyncOperations.IsCacheValid(tempDir, "https://example.com/repo.git", SemVerRange.Parse("^1.0").Value)
                .Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SyncAsync_NullCachedTag_RefetchesTheImport()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"ariadne-provider-{Guid.NewGuid():N}");
        var importDir = Path.Combine(cacheDir, "pack");
        Directory.CreateDirectory(importDir);
        File.WriteAllText(
            Path.Combine(importDir, ".git-info.json"),
            """{ "url": "https://example.com/a.git", "resolvedTag": null, "commitSha": "abc", "syncedAt": "2026-01-01T00:00:00Z" }""");
        var git = new FakeGitScript();
        var import = new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" };

        try
        {
            var result = await new GitCliSyncProvider(new FakeGitClient(git)).SyncAsync(import, cacheDir, noCache: false);

            result.Value.Should().Be(new SyncedImport("pack", "v1.0.0", WasCached: false, importDir));
            git.CloneCalls.Should().Be(1);
        }
        finally
        {
            Directory.Delete(cacheDir, recursive: true);
        }
    }
}
