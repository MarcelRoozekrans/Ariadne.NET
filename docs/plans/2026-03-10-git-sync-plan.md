# Git-Based Sync Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow teams to share rule packs via git repositories (including private repos), with local caching and semver-based version resolution via git tags.

**Architecture:** A standalone `arch sync` CLI command reads `.architecture/imports.yaml`, resolves versions against git tags via `git ls-remote`, and shallow-clones pack files into `.architecture/.cache/git/<name>/`. The system shells out to the `git` CLI for all operations, inheriting the user's existing authentication. `RulePackResolver` gains an `import://` protocol to load cached packs.

**Tech Stack:** .NET 10, System.CommandLine 3.0-preview, YamlDotNet 16.3.0, git CLI via `System.Diagnostics.Process`

---

### Task 1: Import Models

**Files:**
- Create: `src/ArchGov.Core/Sync/ImportDefinition.cs`
- Create: `src/ArchGov.Core/Sync/ImportsManifest.cs`
- Create: `src/ArchGov.Core/Sync/SyncResult.cs`
- Create: `src/ArchGov.Core/Sync/GitInfo.cs`
- Test: `tests/ArchGov.Core.Tests/Sync/ImportDefinitionTests.cs`

**Context:** These are simple data models. `ImportDefinition` represents one entry in `imports.yaml`. `ImportsManifest` holds the list. `SyncResult` captures the outcome of syncing one import. `GitInfo` is the `.git-info.json` cache metadata.

**Step 1: Write the tests**

```csharp
using ArchGov.Core.Sync;
using FluentAssertions;

namespace ArchGov.Core.Tests.Sync;

public class ImportDefinitionTests
{
    [Fact]
    public void ImportDefinition_ShouldHaveRequiredProperties()
    {
        var import = new ImportDefinition
        {
            Name = "company-standards",
            Type = "git",
            Url = "git@github.com:acme/packs.git",
            Version = "^2.0",
            Path = "packs/clean-architecture",
        };

        import.Name.Should().Be("company-standards");
        import.Type.Should().Be("git");
        import.Url.Should().Be("git@github.com:acme/packs.git");
        import.Version.Should().Be("^2.0");
        import.Path.Should().Be("packs/clean-architecture");
    }

    [Fact]
    public void ImportDefinition_Path_ShouldDefaultToEmpty()
    {
        var import = new ImportDefinition
        {
            Name = "test",
            Type = "git",
            Url = "https://github.com/acme/packs.git",
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
                    Name = "pack-a",
                    Type = "git",
                    Url = "https://github.com/a.git",
                    Version = "1.0.0",
                },
                new ImportDefinition
                {
                    Name = "pack-b",
                    Type = "git",
                    Url = "https://github.com/b.git",
                    Version = "^2.0",
                },
            ],
        };

        manifest.Imports.Should().HaveCount(2);
    }

    [Fact]
    public void SyncResult_ShouldCaptureOutcome()
    {
        var result = new SyncResult
        {
            ImportName = "company-standards",
            ResolvedVersion = "2.4.1",
            WasCached = false,
            CachePath = "/tmp/.cache/git/company-standards",
        };

        result.ImportName.Should().Be("company-standards");
        result.ResolvedVersion.Should().Be("2.4.1");
        result.WasCached.Should().BeFalse();
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void SyncResult_WithError_ShouldNotBeSuccess()
    {
        var result = new SyncResult
        {
            ImportName = "broken",
            Success = false,
            Error = "git clone failed",
        };

        result.Success.Should().BeFalse();
        result.Error.Should().Be("git clone failed");
    }

    [Fact]
    public void GitInfo_ShouldCaptureMetadata()
    {
        var info = new GitInfo
        {
            Url = "https://github.com/acme/packs.git",
            ResolvedTag = "v2.4.1",
            CommitSha = "abc123",
            SyncedAt = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
        };

        info.Url.Should().Be("https://github.com/acme/packs.git");
        info.ResolvedTag.Should().Be("v2.4.1");
        info.CommitSha.Should().Be("abc123");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~ImportDefinitionTests" --no-restore`
Expected: FAIL — types do not exist yet.

**Step 3: Implement the models**

Create `src/ArchGov.Core/Sync/ImportDefinition.cs`:
```csharp
namespace ArchGov.Core.Sync;

public sealed class ImportDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Url { get; init; }
    public required string Version { get; init; }
    public string Path { get; init; } = "";
}
```

Create `src/ArchGov.Core/Sync/ImportsManifest.cs`:
```csharp
namespace ArchGov.Core.Sync;

public sealed class ImportsManifest
{
    public IList<ImportDefinition> Imports { get; init; } = [];
}
```

Create `src/ArchGov.Core/Sync/SyncResult.cs`:
```csharp
namespace ArchGov.Core.Sync;

public sealed class SyncResult
{
    public required string ImportName { get; init; }
    public string? ResolvedVersion { get; init; }
    public bool WasCached { get; init; }
    public string? CachePath { get; init; }
    public bool Success { get; init; } = true;
    public string? Error { get; init; }
}
```

Create `src/ArchGov.Core/Sync/GitInfo.cs`:
```csharp
using System.Text.Json.Serialization;

namespace ArchGov.Core.Sync;

public sealed class GitInfo
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("resolvedTag")]
    public required string ResolvedTag { get; init; }

    [JsonPropertyName("commitSha")]
    public required string CommitSha { get; init; }

    [JsonPropertyName("syncedAt")]
    public required DateTimeOffset SyncedAt { get; init; }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~ImportDefinitionTests" --no-restore`
Expected: PASS (6 tests)

**Step 5: Commit**

```bash
git add src/ArchGov.Core/Sync/ tests/ArchGov.Core.Tests/Sync/
git commit -m "feat: add import models for git-based sync"
```

---

### Task 2: ImportsYamlLoader

**Files:**
- Create: `src/ArchGov.Core/Sync/IImportsLoader.cs`
- Create: `src/ArchGov.Core/Sync/ImportsYamlLoader.cs`
- Test: `tests/ArchGov.Core.Tests/Sync/ImportsYamlLoaderTests.cs`

**Context:** This class deserializes `.architecture/imports.yaml` into an `ImportsManifest`. It uses YamlDotNet with `CamelCaseNamingConvention` (same as `YamlRulePackLoader` in `src/ArchGov.Core/RuleLoader/YamlRulePackLoader.cs`).

**Step 1: Write the tests**

```csharp
using ArchGov.Core.Sync;
using FluentAssertions;

namespace ArchGov.Core.Tests.Sync;

public sealed class ImportsYamlLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ImportsYamlLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"archgov-imports-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void LoadFromString_ValidYaml_ShouldReturnManifest()
    {
        const string yaml = """
            imports:
              - name: company-standards
                type: git
                url: git@github.com:acme/packs.git
                version: "^2.0"
                path: packs/clean-arch
              - name: team-rules
                type: git
                url: https://github.com/acme/team.git
                version: "1.3.0"
            """;

        var loader = new ImportsYamlLoader();
        var manifest = loader.LoadFromString(yaml);

        manifest.Imports.Should().HaveCount(2);
        manifest.Imports[0].Name.Should().Be("company-standards");
        manifest.Imports[0].Type.Should().Be("git");
        manifest.Imports[0].Url.Should().Be("git@github.com:acme/packs.git");
        manifest.Imports[0].Version.Should().Be("^2.0");
        manifest.Imports[0].Path.Should().Be("packs/clean-arch");
        manifest.Imports[1].Name.Should().Be("team-rules");
        manifest.Imports[1].Path.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromString_EmptyImports_ShouldReturnEmptyList()
    {
        const string yaml = """
            imports: []
            """;

        var loader = new ImportsYamlLoader();
        var manifest = loader.LoadFromString(yaml);

        manifest.Imports.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromFile_ShouldReadAndParse()
    {
        var filePath = Path.Combine(_tempDir, "imports.yaml");
        File.WriteAllText(filePath, """
            imports:
              - name: test-pack
                type: git
                url: https://github.com/test/pack.git
                version: "1.0.0"
            """);

        var loader = new ImportsYamlLoader();
        var manifest = loader.LoadFromFile(filePath);

        manifest.Imports.Should().ContainSingle();
        manifest.Imports[0].Name.Should().Be("test-pack");
    }

    [Fact]
    public void LoadFromFile_FileNotFound_ShouldThrow()
    {
        var loader = new ImportsYamlLoader();

        var act = () => loader.LoadFromFile(Path.Combine(_tempDir, "nope.yaml"));

        act.Should().Throw<FileNotFoundException>();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~ImportsYamlLoaderTests" --no-restore`
Expected: FAIL — types do not exist yet.

**Step 3: Implement**

Create `src/ArchGov.Core/Sync/IImportsLoader.cs`:
```csharp
namespace ArchGov.Core.Sync;

public interface IImportsLoader
{
    ImportsManifest LoadFromString(string yaml);
    ImportsManifest LoadFromFile(string filePath);
}
```

Create `src/ArchGov.Core/Sync/ImportsYamlLoader.cs`:
```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchGov.Core.Sync;

public sealed class ImportsYamlLoader : IImportsLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ImportsManifest LoadFromString(string yaml)
    {
        return _deserializer.Deserialize<ImportsManifest>(yaml);
    }

    public ImportsManifest LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Imports file not found: {filePath}", filePath);
        }

        var yaml = File.ReadAllText(filePath);
        return LoadFromString(yaml);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~ImportsYamlLoaderTests" --no-restore`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/ArchGov.Core/Sync/IImportsLoader.cs src/ArchGov.Core/Sync/ImportsYamlLoader.cs tests/ArchGov.Core.Tests/Sync/ImportsYamlLoaderTests.cs
git commit -m "feat: add imports.yaml loader for git sync"
```

---

### Task 3: SemVer Range Resolution

**Files:**
- Create: `src/ArchGov.Core/Sync/SemVerRange.cs`
- Test: `tests/ArchGov.Core.Tests/Sync/SemVerRangeTests.cs`

**Context:** This class parses semver version strings and range constraints (`^2.0`, `~1.3`, `>=1.0 <2.0`, exact `1.3.0`). It can find the best matching version from a list of candidates. It does NOT need to be a full npm-style semver library — just the common patterns. Git tags may have a `v` prefix (e.g., `v1.0.0`) which should be stripped.

**Step 1: Write the tests**

```csharp
using ArchGov.Core.Sync;
using FluentAssertions;

namespace ArchGov.Core.Tests.Sync;

public class SemVerRangeTests
{
    [Theory]
    [InlineData("v1.0.0", 1, 0, 0)]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("v2.3.1", 2, 3, 1)]
    [InlineData("0.1.0", 0, 1, 0)]
    public void TryParse_ValidVersions_ShouldParse(string input, int major, int minor, int patch)
    {
        var result = SemVer.TryParse(input, out var version);

        result.Should().BeTrue();
        version!.Major.Should().Be(major);
        version.Minor.Should().Be(minor);
        version.Patch.Should().Be(patch);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("latest")]
    [InlineData("refs/tags/foo")]
    public void TryParse_InvalidVersions_ShouldReturnFalse(string input)
    {
        SemVer.TryParse(input, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "2.0.0", false)]
    public void ExactMatch_ShouldMatchExactly(string range, string candidate, bool expected)
    {
        var semverRange = SemVerRange.Parse(range);
        SemVer.TryParse(candidate, out var version);

        semverRange.IsSatisfiedBy(version!).Should().Be(expected);
    }

    [Theory]
    [InlineData("^1.0.0", "1.0.0", true)]
    [InlineData("^1.0.0", "1.9.9", true)]
    [InlineData("^1.0.0", "2.0.0", false)]
    [InlineData("^1.2.0", "1.2.0", true)]
    [InlineData("^1.2.0", "1.3.0", true)]
    [InlineData("^1.2.0", "1.1.0", false)]
    [InlineData("^0.1.0", "0.1.0", true)]
    [InlineData("^0.1.0", "0.1.5", true)]
    [InlineData("^0.1.0", "0.2.0", false)]
    public void CaretRange_ShouldAllowMinorAndPatchBumps(string range, string candidate, bool expected)
    {
        var semverRange = SemVerRange.Parse(range);
        SemVer.TryParse(candidate, out var version);

        semverRange.IsSatisfiedBy(version!).Should().Be(expected);
    }

    [Theory]
    [InlineData("~1.2.0", "1.2.0", true)]
    [InlineData("~1.2.0", "1.2.9", true)]
    [InlineData("~1.2.0", "1.3.0", false)]
    [InlineData("~1.2.0", "1.1.0", false)]
    public void TildeRange_ShouldAllowPatchBumpsOnly(string range, string candidate, bool expected)
    {
        var semverRange = SemVerRange.Parse(range);
        SemVer.TryParse(candidate, out var version);

        semverRange.IsSatisfiedBy(version!).Should().Be(expected);
    }

    [Theory]
    [InlineData("^2.0", "v2.0.0", "v2.1.0", "v2.4.1", "v3.0.0", "v2.4.1")]
    [InlineData("~1.2", "v1.2.0", "v1.2.3", "v1.3.0", "v1.1.0", "v1.2.3")]
    [InlineData("1.0.0", "v1.0.0", "v1.0.1", "v2.0.0", "v0.9.0", "v1.0.0")]
    public void FindBestMatch_ShouldReturnHighestSatisfying(
        string range, string t1, string t2, string t3, string t4, string expectedTag)
    {
        var tags = new[] { t1, t2, t3, t4 };
        var semverRange = SemVerRange.Parse(range);

        var best = semverRange.FindBestMatch(tags);

        best.Should().Be(expectedTag);
    }

    [Fact]
    public void FindBestMatch_NoMatch_ShouldReturnNull()
    {
        var tags = new[] { "v3.0.0", "v4.0.0" };
        var semverRange = SemVerRange.Parse("^1.0");

        semverRange.FindBestMatch(tags).Should().BeNull();
    }

    [Theory]
    [InlineData("^2.0")]
    [InlineData("~1.3")]
    [InlineData("1.0.0")]
    public void Parse_ValidRanges_ShouldNotThrow(string range)
    {
        var act = () => SemVerRange.Parse(range);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-valid")]
    public void Parse_InvalidRanges_ShouldThrow(string range)
    {
        var act = () => SemVerRange.Parse(range);
        act.Should().Throw<FormatException>();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SemVerRangeTests" --no-restore`
Expected: FAIL — types do not exist yet.

**Step 3: Implement**

Create `src/ArchGov.Core/Sync/SemVerRange.cs`:
```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace ArchGov.Core.Sync;

public sealed partial class SemVer : IComparable<SemVer>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public SemVer(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static bool TryParse(string input, out SemVer? version)
    {
        version = null;
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        var match = SemVerPattern().Match(input);
        if (!match.Success)
        {
            return false;
        }

        var major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var patch = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : 0;

        version = new SemVer(major, minor, patch);
        return true;
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        return result != 0 ? result : Patch.CompareTo(other.Patch);
    }

    [GeneratedRegex(@"^v?(\d+)\.(\d+)(?:\.(\d+))?$")]
    private static partial Regex SemVerPattern();
}

public sealed class SemVerRange
{
    private readonly Func<SemVer, bool> _predicate;

    private SemVerRange(Func<SemVer, bool> predicate)
    {
        _predicate = predicate;
    }

    public bool IsSatisfiedBy(SemVer version) => _predicate(version);

    public string? FindBestMatch(IEnumerable<string> tags)
    {
        string? bestTag = null;
        SemVer? bestVersion = null;

        foreach (var tag in tags)
        {
            if (!SemVer.TryParse(tag, out var version))
            {
                continue;
            }

            if (!IsSatisfiedBy(version))
            {
                continue;
            }

            if (bestVersion is null || version.CompareTo(bestVersion) > 0)
            {
                bestVersion = version;
                bestTag = tag;
            }
        }

        return bestTag;
    }

    public static SemVerRange Parse(string range)
    {
        if (string.IsNullOrEmpty(range))
        {
            throw new FormatException($"Invalid semver range: '{range}'");
        }

        if (range.StartsWith('^'))
        {
            return ParseCaret(range[1..]);
        }

        if (range.StartsWith('~'))
        {
            return ParseTilde(range[1..]);
        }

        // Exact version match
        if (!SemVer.TryParse(range, out var exact))
        {
            throw new FormatException($"Invalid semver range: '{range}'");
        }

        return new SemVerRange(v =>
            v.Major == exact.Major && v.Minor == exact.Minor && v.Patch == exact.Patch);
    }

    private static SemVerRange ParseCaret(string versionPart)
    {
        if (!SemVer.TryParse(versionPart, out var min))
        {
            throw new FormatException($"Invalid semver range: '^{versionPart}'");
        }

        // ^0.x.y: only allow patch bumps (0.x.y to 0.x.*)
        // ^X.y.z (X>0): allow minor+patch bumps (X.y.z to X.*.*)
        if (min.Major == 0)
        {
            return new SemVerRange(v =>
                v.Major == 0
                && v.Minor == min.Minor
                && v.Patch >= min.Patch);
        }

        return new SemVerRange(v =>
            v.Major == min.Major
            && v.CompareTo(min) >= 0);
    }

    private static SemVerRange ParseTilde(string versionPart)
    {
        if (!SemVer.TryParse(versionPart, out var min))
        {
            throw new FormatException($"Invalid semver range: '~{versionPart}'");
        }

        // ~X.Y.Z: allow patch bumps only (X.Y.Z to X.Y.*)
        return new SemVerRange(v =>
            v.Major == min.Major
            && v.Minor == min.Minor
            && v.Patch >= min.Patch);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SemVerRangeTests" --no-restore`
Expected: PASS (all tests)

**Step 5: Commit**

```bash
git add src/ArchGov.Core/Sync/SemVerRange.cs tests/ArchGov.Core.Tests/Sync/SemVerRangeTests.cs
git commit -m "feat: add semver range parsing and matching for git tag resolution"
```

---

### Task 4: Git Sync Provider

**Files:**
- Create: `src/ArchGov.Core/Sync/IGitSyncProvider.cs`
- Create: `src/ArchGov.Core/Sync/GitCliSyncProvider.cs`
- Test: `tests/ArchGov.Core.Tests/Sync/GitCliSyncProviderTests.cs`

**Context:** This is the core service that interacts with git. It shells out to the `git` CLI. For unit tests, we test the tag-parsing and version-resolution logic by mocking the process execution. The actual git calls are tested in integration tests (Task 7).

The provider:
1. Lists remote tags via `git ls-remote --tags <url>`
2. Resolves the best version from tags using `SemVerRange`
3. Shallow-clones the resolved tag into the cache directory
4. Writes `.git-info.json` with metadata
5. Skips clone if cache is fresh and version still satisfies the constraint

**Step 1: Write the tests**

```csharp
using ArchGov.Core.Sync;
using FluentAssertions;

namespace ArchGov.Core.Tests.Sync;

public class GitCliSyncProviderTests
{
    [Fact]
    public void ParseTagsFromLsRemote_ShouldExtractTagNames()
    {
        var lsRemoteOutput = """
            abc123def456	refs/tags/v1.0.0
            bcd234efa567	refs/tags/v1.1.0
            cde345fab678	refs/tags/v2.0.0
            def456abc789	refs/tags/v2.0.0^{}
            efg567bcd890	refs/tags/not-semver
            """;

        var tags = GitCliSyncProvider.ParseTagsFromLsRemote(lsRemoteOutput);

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
        var lsRemoteOutput = """
            abc123def456	refs/tags/v1.0.0
            bcd234efa567	refs/tags/v1.1.0
            cde345fab678	refs/tags/v2.0.0
            """;

        var sha = GitCliSyncProvider.ParseCommitFromLsRemote(lsRemoteOutput, "v2.0.0");
        sha.Should().Be("cde345fab678");
    }

    [Fact]
    public void ParseCommitFromLsRemote_DereferencedTag_ShouldPreferDereferenced()
    {
        var lsRemoteOutput = """
            abc123def456	refs/tags/v1.0.0
            bcd234efa567	refs/tags/v1.0.0^{}
            """;

        var sha = GitCliSyncProvider.ParseCommitFromLsRemote(lsRemoteOutput, "v1.0.0");
        sha.Should().Be("bcd234efa567");
    }

    [Fact]
    public void IsCacheValid_NoCacheDir_ShouldReturnFalse()
    {
        var result = GitCliSyncProvider.IsCacheValid(
            "/nonexistent/path",
            "https://github.com/test.git",
            SemVerRange.Parse("^1.0"));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsCacheValid_ValidCache_ShouldReturnTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var gitInfo = new GitInfo
            {
                Url = "https://github.com/test.git",
                ResolvedTag = "v1.2.0",
                CommitSha = "abc123",
                SyncedAt = DateTimeOffset.UtcNow,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(gitInfo);
            File.WriteAllText(Path.Combine(tempDir, ".git-info.json"), json);

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://github.com/test.git",
                SemVerRange.Parse("^1.0"));

            result.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsCacheValid_DifferentUrl_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var gitInfo = new GitInfo
            {
                Url = "https://github.com/other.git",
                ResolvedTag = "v1.2.0",
                CommitSha = "abc123",
                SyncedAt = DateTimeOffset.UtcNow,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(gitInfo);
            File.WriteAllText(Path.Combine(tempDir, ".git-info.json"), json);

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://github.com/test.git",
                SemVerRange.Parse("^1.0"));

            result.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsCacheValid_VersionNotSatisfied_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var gitInfo = new GitInfo
            {
                Url = "https://github.com/test.git",
                ResolvedTag = "v1.2.0",
                CommitSha = "abc123",
                SyncedAt = DateTimeOffset.UtcNow,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(gitInfo);
            File.WriteAllText(Path.Combine(tempDir, ".git-info.json"), json);

            var result = GitCliSyncProvider.IsCacheValid(
                tempDir,
                "https://github.com/test.git",
                SemVerRange.Parse("^2.0"));

            result.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~GitCliSyncProviderTests" --no-restore`
Expected: FAIL — types do not exist yet.

**Step 3: Implement**

Create `src/ArchGov.Core/Sync/IGitSyncProvider.cs`:
```csharp
namespace ArchGov.Core.Sync;

public interface IGitSyncProvider
{
    Task<SyncResult> SyncAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct = default);
}
```

Create `src/ArchGov.Core/Sync/GitCliSyncProvider.cs`:
```csharp
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ArchGov.Core.Sync;

public sealed class GitCliSyncProvider : IGitSyncProvider
{
    public async Task<SyncResult> SyncAsync(
        ImportDefinition import,
        string cacheDir,
        bool noCache,
        CancellationToken ct = default)
    {
        var importCacheDir = Path.Combine(cacheDir, import.Name);
        var versionRange = SemVerRange.Parse(import.Version);

        // Check cache validity
        if (!noCache && IsCacheValid(importCacheDir, import.Url, versionRange))
        {
            var cachedInfo = ReadGitInfo(importCacheDir);
            return new SyncResult
            {
                ImportName = import.Name,
                ResolvedVersion = cachedInfo?.ResolvedTag,
                WasCached = true,
                CachePath = importCacheDir,
            };
        }

        try
        {
            // List remote tags
            var lsRemoteOutput = await RunGitAsync($"ls-remote --tags {import.Url}", ct);
            var tags = ParseTagsFromLsRemote(lsRemoteOutput);

            // Resolve best version
            var bestTag = versionRange.FindBestMatch(tags);
            if (bestTag is null)
            {
                return new SyncResult
                {
                    ImportName = import.Name,
                    Success = false,
                    Error = $"No git tag satisfies version constraint '{import.Version}' for '{import.Name}'",
                };
            }

            var commitSha = ParseCommitFromLsRemote(lsRemoteOutput, bestTag) ?? "unknown";

            // Clean and clone
            if (Directory.Exists(importCacheDir))
            {
                Directory.Delete(importCacheDir, true);
            }

            Directory.CreateDirectory(importCacheDir);

            // Shallow clone at specific tag
            var cloneTarget = Path.Combine(importCacheDir, ".clone-temp");
            await RunGitAsync(
                $"clone --depth 1 --branch {bestTag} --single-branch {import.Url} \"{cloneTarget}\"",
                ct);

            // Copy pack files from the path (or root) to cache dir
            var sourceDir = string.IsNullOrEmpty(import.Path)
                ? cloneTarget
                : Path.Combine(cloneTarget, import.Path);

            if (!Directory.Exists(sourceDir))
            {
                return new SyncResult
                {
                    ImportName = import.Name,
                    Success = false,
                    Error = $"Path '{import.Path}' not found in repository '{import.Url}' at tag '{bestTag}'",
                };
            }

            CopyPackFiles(sourceDir, importCacheDir);

            // Clean up clone temp
            Directory.Delete(cloneTarget, true);

            // Write cache metadata
            var gitInfo = new GitInfo
            {
                Url = import.Url,
                ResolvedTag = bestTag,
                CommitSha = commitSha,
                SyncedAt = DateTimeOffset.UtcNow,
            };

            var json = JsonSerializer.Serialize(gitInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(
                Path.Combine(importCacheDir, ".git-info.json"),
                json,
                ct);

            return new SyncResult
            {
                ImportName = import.Name,
                ResolvedVersion = bestTag,
                WasCached = false,
                CachePath = importCacheDir,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SyncResult
            {
                ImportName = import.Name,
                Success = false,
                Error = $"Git sync failed for '{import.Name}': {ex.Message}",
            };
        }
    }

    public static IReadOnlyList<string> ParseTagsFromLsRemote(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var tags = new List<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2)
            {
                continue;
            }

            var refName = parts[1].Trim();

            // Skip dereferenced tag markers (^{})
            if (refName.EndsWith("^{}", StringComparison.Ordinal))
            {
                continue;
            }

            if (!refName.StartsWith("refs/tags/", StringComparison.Ordinal))
            {
                continue;
            }

            var tagName = refName["refs/tags/".Length..];

            // Only include semver-compatible tags
            if (SemVer.TryParse(tagName, out _))
            {
                tags.Add(tagName);
            }
        }

        return tags;
    }

    public static string? ParseCommitFromLsRemote(string output, string tagName)
    {
        string? sha = null;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2)
            {
                continue;
            }

            var refName = parts[1].Trim();

            // Prefer dereferenced tag (^{}) if available
            if (refName == $"refs/tags/{tagName}^{{}}")
            {
                return parts[0].Trim();
            }

            if (refName == $"refs/tags/{tagName}")
            {
                sha = parts[0].Trim();
            }
        }

        return sha;
    }

    public static bool IsCacheValid(string cacheDir, string expectedUrl, SemVerRange versionRange)
    {
        var gitInfo = ReadGitInfo(cacheDir);
        if (gitInfo is null)
        {
            return false;
        }

        if (!string.Equals(gitInfo.Url, expectedUrl, StringComparison.Ordinal))
        {
            return false;
        }

        if (!SemVer.TryParse(gitInfo.ResolvedTag, out var cachedVersion))
        {
            return false;
        }

        return versionRange.IsSatisfiedBy(cachedVersion);
    }

    private static GitInfo? ReadGitInfo(string cacheDir)
    {
        var infoPath = Path.Combine(cacheDir, ".git-info.json");
        if (!File.Exists(infoPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(infoPath);
            return JsonSerializer.Deserialize<GitInfo>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void CopyPackFiles(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            // Skip .git directory contents
            if (file.Contains(Path.Combine(sourceDir, ".git"), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetPath = Path.Combine(targetDir, relativePath);
            var targetFileDir = Path.GetDirectoryName(targetPath)!;

            if (!Directory.Exists(targetFileDir))
            {
                Directory.CreateDirectory(targetFileDir);
            }

            File.Copy(file, targetPath, overwrite: true);
        }
    }

    private static async Task<string> RunGitAsync(string arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "git {0} failed (exit code {1}): {2}", arguments, process.ExitCode, error));
        }

        return output;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~GitCliSyncProviderTests" --no-restore`
Expected: PASS (7 tests)

**Step 5: Commit**

```bash
git add src/ArchGov.Core/Sync/IGitSyncProvider.cs src/ArchGov.Core/Sync/GitCliSyncProvider.cs tests/ArchGov.Core.Tests/Sync/GitCliSyncProviderTests.cs
git commit -m "feat: add git CLI sync provider with tag resolution and caching"
```

---

### Task 5: Sync Orchestrator

**Files:**
- Create: `src/ArchGov.Core/Sync/ISyncOrchestrator.cs`
- Create: `src/ArchGov.Core/Sync/SyncOrchestrator.cs`
- Test: `tests/ArchGov.Core.Tests/Sync/SyncOrchestratorTests.cs`

**Context:** This service coordinates syncing all imports from a manifest. It iterates imports, calls `IGitSyncProvider` for each, and collects results. It also supports filtering by name (`--name` flag). This is the service the CLI command calls.

**Step 1: Write the tests**

```csharp
using ArchGov.Core.Sync;
using FluentAssertions;
using NSubstitute;

namespace ArchGov.Core.Tests.Sync;

public class SyncOrchestratorTests
{
    [Fact]
    public async Task SyncAllAsync_ShouldSyncEachImport()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                ResolvedVersion = "v1.0.0",
                WasCached = false,
                CachePath = "/cache",
            }));

        var orchestrator = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://a.git", Version = "1.0.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://b.git", Version = "^2.0" },
            ],
        };

        var results = await orchestrator.SyncAllAsync(manifest, "/cache", noCache: false);

        results.Should().HaveCount(2);
        results[0].ImportName.Should().Be("pack-a");
        results[1].ImportName.Should().Be("pack-b");
    }

    [Fact]
    public async Task SyncByNameAsync_ShouldSyncOnlyMatchingImport()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                ResolvedVersion = "v1.0.0",
                WasCached = false,
                CachePath = "/cache",
            }));

        var orchestrator = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://a.git", Version = "1.0.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://b.git", Version = "^2.0" },
            ],
        };

        var results = await orchestrator.SyncByNameAsync(manifest, "pack-b", "/cache", noCache: false);

        results.Should().ContainSingle();
        results[0].ImportName.Should().Be("pack-b");
    }

    [Fact]
    public async Task SyncByNameAsync_NameNotFound_ShouldReturnError()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        var orchestrator = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://a.git", Version = "1.0.0" },
            ],
        };

        var results = await orchestrator.SyncByNameAsync(manifest, "nonexistent", "/cache", noCache: false);

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("nonexistent");
    }

    [Fact]
    public async Task SyncAllAsync_UnsupportedType_ShouldReturnError()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        var orchestrator = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "nuget-pack", Type = "nuget", Url = "https://nuget.org", Version = "1.0.0" },
            ],
        };

        var results = await orchestrator.SyncAllAsync(manifest, "/cache", noCache: false);

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("nuget");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SyncOrchestratorTests" --no-restore`
Expected: FAIL — types do not exist yet.

**Note:** You may need to add `NSubstitute` to the test project if not already present:
```bash
dotnet add tests/ArchGov.Core.Tests package NSubstitute
```

**Step 3: Implement**

Create `src/ArchGov.Core/Sync/ISyncOrchestrator.cs`:
```csharp
namespace ArchGov.Core.Sync;

public interface ISyncOrchestrator
{
    Task<IReadOnlyList<SyncResult>> SyncAllAsync(ImportsManifest manifest, string cacheDir, bool noCache, CancellationToken ct = default);
    Task<IReadOnlyList<SyncResult>> SyncByNameAsync(ImportsManifest manifest, string name, string cacheDir, bool noCache, CancellationToken ct = default);
}
```

Create `src/ArchGov.Core/Sync/SyncOrchestrator.cs`:
```csharp
namespace ArchGov.Core.Sync;

public sealed class SyncOrchestrator(IGitSyncProvider gitProvider) : ISyncOrchestrator
{
    public async Task<IReadOnlyList<SyncResult>> SyncAllAsync(
        ImportsManifest manifest,
        string cacheDir,
        bool noCache,
        CancellationToken ct = default)
    {
        var results = new List<SyncResult>();

        foreach (var import in manifest.Imports)
        {
            var result = await SyncSingleAsync(import, cacheDir, noCache, ct);
            results.Add(result);
        }

        return results;
    }

    public async Task<IReadOnlyList<SyncResult>> SyncByNameAsync(
        ImportsManifest manifest,
        string name,
        string cacheDir,
        bool noCache,
        CancellationToken ct = default)
    {
        var import = manifest.Imports.FirstOrDefault(
            i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

        if (import is null)
        {
            return
            [
                new SyncResult
                {
                    ImportName = name,
                    Success = false,
                    Error = $"Import '{name}' not found in imports.yaml",
                },
            ];
        }

        var result = await SyncSingleAsync(import, cacheDir, noCache, ct);
        return [result];
    }

    private async Task<SyncResult> SyncSingleAsync(
        ImportDefinition import,
        string cacheDir,
        bool noCache,
        CancellationToken ct)
    {
        if (!string.Equals(import.Type, "git", StringComparison.OrdinalIgnoreCase))
        {
            return new SyncResult
            {
                ImportName = import.Name,
                Success = false,
                Error = $"Unsupported import type '{import.Type}' for '{import.Name}'. Only 'git' is currently supported.",
            };
        }

        return await gitProvider.SyncAsync(import, cacheDir, noCache, ct);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~SyncOrchestratorTests" --no-restore`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add src/ArchGov.Core/Sync/ISyncOrchestrator.cs src/ArchGov.Core/Sync/SyncOrchestrator.cs tests/ArchGov.Core.Tests/Sync/SyncOrchestratorTests.cs
git commit -m "feat: add sync orchestrator to coordinate import syncing"
```

---

### Task 6: DI Registration and RulePackResolver Integration

**Files:**
- Modify: `src/ArchGov.Core/ServiceCollectionExtensions.cs`
- Modify: `src/ArchGov.Core/RuleLoader/RulePackResolver.cs`
- Test: `tests/ArchGov.Core.Tests/RuleLoader/RulePackResolverTests.cs` (add new tests)

**Context:** Register the new sync services in DI. Extend `RulePackResolver` to support `import://` references that load from `.architecture/.cache/git/<name>/`. Check the existing `RulePackResolverTests.cs` for the current test pattern.

**Step 1: Read existing resolver tests**

Read `tests/ArchGov.Core.Tests/RuleLoader/RulePackResolverTests.cs` to understand the current test pattern before adding new tests.

**Step 2: Write the failing test for import:// resolution**

Add to `tests/ArchGov.Core.Tests/RuleLoader/RulePackResolverTests.cs`:

```csharp
[Fact]
public void Resolve_ImportProtocol_ShouldLoadFromCacheDirectory()
{
    // Set up a cached pack in a temp directory
    var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-resolver-{Guid.NewGuid():N}");
    var cacheDir = Path.Combine(tempDir, ".architecture", ".cache", "git", "my-pack");
    Directory.CreateDirectory(cacheDir);

    try
    {
        File.WriteAllText(Path.Combine(cacheDir, "my-pack.yaml"), """
            name: my-pack
            layers:
              - name: Domain
                allowedDependencies: []
              - name: Application
                allowedDependencies:
                  - Domain
            """);

        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var merger = new RulePackMerger();
        var resolver = new RulePackResolver(registry, loader, merger);

        var userPack = new RulePack
        {
            Name = "user-rules",
            Extends = ["import://my-pack"],
        };

        var result = resolver.Resolve(userPack, tempDir);

        result.Layers.Should().Contain(l => l.Name == "Domain");
        result.Layers.Should().Contain(l => l.Name == "Application");
    }
    finally
    {
        Directory.Delete(tempDir, true);
    }
}

[Fact]
public void Resolve_ImportProtocol_NoCacheDir_ShouldThrow()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"archgov-resolver-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var merger = new RulePackMerger();
        var resolver = new RulePackResolver(registry, loader, merger);

        var userPack = new RulePack
        {
            Name = "user-rules",
            Extends = ["import://nonexistent"],
        };

        var act = () => resolver.Resolve(userPack, tempDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sync*");
    }
    finally
    {
        Directory.Delete(tempDir, true);
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~RulePackResolverTests" --no-restore`
Expected: FAIL — `Resolve` doesn't accept `workingDirectory` parameter yet.

**Step 4: Update RulePackResolver**

Modify `src/ArchGov.Core/RuleLoader/RulePackResolver.cs`. The `Resolve` method needs an optional `workingDirectory` parameter. Add an overload to avoid breaking existing callers:

```csharp
// Add this constant at class level
private const string ImportPrefix = "import://";

// Add overload
public RulePack Resolve(RulePack userPack, string? workingDirectory)
{
    ArgumentNullException.ThrowIfNull(userPack);

    if (userPack.Extends.Count == 0)
    {
        return userPack;
    }

    var basePacks = new List<RulePack>();
    foreach (var reference in userPack.Extends)
    {
        if (reference.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var packName = reference[BuiltinPrefix.Length..];
            basePacks.Add(_registry.GetPack(packName));
        }
        else if (reference.StartsWith(ImportPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var importName = reference[ImportPrefix.Length..];
            basePacks.Add(LoadImportedPack(importName, workingDirectory));
        }
        else
        {
            throw new NotSupportedException($"Unsupported pack reference: '{reference}'. Supported: 'builtin://', 'import://'");
        }
    }

    basePacks.Add(userPack);
    return _merger.Merge(basePacks);
}

private RulePack LoadImportedPack(string importName, string? workingDirectory)
{
    if (string.IsNullOrEmpty(workingDirectory))
    {
        throw new InvalidOperationException(
            $"Cannot resolve import '{importName}': no working directory specified. Run 'arch sync' first.");
    }

    var cacheDir = Path.Combine(workingDirectory, ".architecture", ".cache", "git", importName);
    if (!Directory.Exists(cacheDir))
    {
        throw new InvalidOperationException(
            $"Import '{importName}' not found in cache. Run 'arch sync' to fetch it.");
    }

    // Find the YAML file in the cache directory
    var yamlFiles = Directory.GetFiles(cacheDir, "*.yaml", SearchOption.TopDirectoryOnly);
    if (yamlFiles.Length == 0)
    {
        throw new InvalidOperationException(
            $"No YAML pack files found in cache for import '{importName}'. Run 'arch sync' to re-fetch.");
    }

    // Load the first yaml file found (packs have a single entry point)
    return _loader.LoadFromFile(yamlFiles[0]);
}
```

Also update the existing `Resolve(RulePack userPack)` overload to call the new one:
```csharp
public RulePack Resolve(RulePack userPack)
{
    return Resolve(userPack, workingDirectory: null);
}
```

Update the `IRulePackResolver` interface to add the new overload:
```csharp
public interface IRulePackResolver
{
    RulePack Resolve(RulePack userPack);
    RulePack Resolve(RulePack userPack, string? workingDirectory);
}
```

**Step 5: Update DI registration**

Add to `src/ArchGov.Core/ServiceCollectionExtensions.cs`:
```csharp
// Add these registrations alongside existing ones:
services.AddSingleton<IImportsLoader, ImportsYamlLoader>();
services.AddSingleton<IGitSyncProvider, GitCliSyncProvider>();
services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
```

Add the using:
```csharp
using ArchGov.Core.Sync;
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~RulePackResolverTests" --no-restore`
Expected: PASS

Run all tests to verify no regressions:
Run: `dotnet test --no-restore`
Expected: All tests PASS

**Step 7: Commit**

```bash
git add src/ArchGov.Core/ServiceCollectionExtensions.cs src/ArchGov.Core/RuleLoader/RulePackResolver.cs src/ArchGov.Core/RuleLoader/IRulePackResolver.cs tests/ArchGov.Core.Tests/RuleLoader/RulePackResolverTests.cs
git commit -m "feat: add import:// protocol to RulePackResolver and register sync services"
```

---

### Task 7: Sync CLI Command

**Files:**
- Create: `src/ArchGov.Host.Cli/Commands/SyncCommand.cs`
- Modify: `src/ArchGov.Host.Cli/Program.cs`

**Context:** Follow the existing command pattern in `src/ArchGov.Host.Cli/Commands/`. Commands define a static `Create(IServiceProvider provider)` method that returns a `Command`. See `ValidateCommand.cs` or `LintCommand.cs` for examples.

**Step 1: Create SyncCommand**

Create `src/ArchGov.Host.Cli/Commands/SyncCommand.cs`:
```csharp
using System.CommandLine;
using ArchGov.Core.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace ArchGov.Host.Cli.Commands;

internal static class SyncCommand
{
    private const string DefaultImportsPath = ".architecture/imports.yaml";
    private const string DefaultCacheDir = ".architecture/.cache/git";

    public static Command Create(IServiceProvider provider)
    {
        var noCacheOption = new Option<bool>(
            ["--no-cache"],
            "Force fresh sync, ignore cached versions");

        var nameOption = new Option<string?>(
            ["--name", "-n"],
            "Sync only the import with this name");

        var command = new Command("sync", "Sync rule packs from git repositories defined in imports.yaml")
        {
            noCacheOption,
            nameOption,
        };

        command.SetAction(async (parseResult, ct) =>
        {
            var noCache = parseResult.GetValue(noCacheOption);
            var name = parseResult.GetValue(nameOption);

            var workingDir = Directory.GetCurrentDirectory();
            var importsPath = Path.Combine(workingDir, DefaultImportsPath);

            if (!File.Exists(importsPath))
            {
                Console.Error.WriteLine($"No imports file found at {importsPath}");
                Console.Error.WriteLine("Create .architecture/imports.yaml to define external rule packs.");
                return;
            }

            var loader = provider.GetRequiredService<IImportsLoader>();
            var orchestrator = provider.GetRequiredService<ISyncOrchestrator>();

            var manifest = loader.LoadFromFile(importsPath);

            if (manifest.Imports.Count == 0)
            {
                Console.WriteLine("No imports defined in imports.yaml.");
                return;
            }

            var cacheDir = Path.Combine(workingDir, DefaultCacheDir);
            Directory.CreateDirectory(cacheDir);

            IReadOnlyList<SyncResult> results;

            if (!string.IsNullOrEmpty(name))
            {
                Console.WriteLine($"Syncing import '{name}'...");
                results = await orchestrator.SyncByNameAsync(manifest, name, cacheDir, noCache, ct);
            }
            else
            {
                Console.WriteLine($"Syncing {manifest.Imports.Count} import(s)...");
                results = await orchestrator.SyncAllAsync(manifest, cacheDir, noCache, ct);
            }

            PrintResults(results);
        });

        return command;
    }

    private static void PrintResults(IReadOnlyList<SyncResult> results)
    {
        var successCount = 0;
        var failCount = 0;

        foreach (var result in results)
        {
            if (result.Success)
            {
                var status = result.WasCached ? "cached" : "fetched";
                Console.WriteLine($"  {result.ImportName,-30} {result.ResolvedVersion,-12} ({status})");
                successCount++;
            }
            else
            {
                Console.Error.WriteLine($"  {result.ImportName,-30} FAILED: {result.Error}");
                failCount++;
            }
        }

        Console.WriteLine();
        if (failCount == 0)
        {
            Console.WriteLine($"Done. {successCount} pack(s) synced.");
        }
        else
        {
            Console.Error.WriteLine($"Done. {successCount} synced, {failCount} failed.");
        }
    }
}
```

**Step 2: Register in Program.cs**

Modify `src/ArchGov.Host.Cli/Program.cs`. Add after the existing command registrations:

```csharp
rootCommand.Subcommands.Add(SyncCommand.Create(provider));
```

**Step 3: Build and verify**

Run: `dotnet build --no-restore`
Expected: Build succeeds with 0 errors.

Run: `dotnet run --project src/ArchGov.Host.Cli -- sync --help`
Expected: Shows sync command help text.

**Step 4: Commit**

```bash
git add src/ArchGov.Host.Cli/Commands/SyncCommand.cs src/ArchGov.Host.Cli/Program.cs
git commit -m "feat: add arch sync CLI command"
```

---

### Task 8: Update RulesCommand to Pass Working Directory

**Files:**
- Modify: `src/ArchGov.Host.Cli/Commands/RulesCommand.cs`

**Context:** The `RulesCommand.ResolveRulesFromFile()` helper is used by `ValidateCommand`, `LintCommand`, and others. It needs to pass the working directory to `RulePackResolver.Resolve()` so that `import://` references can be resolved. Check current implementation at `src/ArchGov.Host.Cli/Commands/RulesCommand.cs:43-59`.

**Step 1: Read current RulesCommand**

Read `src/ArchGov.Host.Cli/Commands/RulesCommand.cs` to see current `ResolveRulesFromFile`.

**Step 2: Update ResolveRulesFromFile to pass working directory**

Modify the method to call `resolver.Resolve(rawPack, workingDir)`:

```csharp
internal static RulePack ResolveRulesFromFile(IServiceProvider provider)
{
    var rulesPath = Path.Combine(Directory.GetCurrentDirectory(), RulesFilePath);

    if (!File.Exists(rulesPath))
    {
        throw new FileNotFoundException($"Rules file not found: {rulesPath}. Run 'arch init' first.");
    }

    var loader = provider.GetRequiredService<IRulePackLoader>();
    var resolver = provider.GetRequiredService<IRulePackResolver>();

    var rawPack = loader.LoadFromFile(rulesPath);
    return resolver.Resolve(rawPack, Directory.GetCurrentDirectory());
}
```

**Step 3: Build and run all tests**

Run: `dotnet build --no-restore && dotnet test --no-restore`
Expected: Build succeeds, all tests pass.

**Step 4: Commit**

```bash
git add src/ArchGov.Host.Cli/Commands/RulesCommand.cs
git commit -m "feat: pass working directory to resolver for import:// support"
```

---

### Task 9: End-to-End Integration Test

**Files:**
- Create: `tests/ArchGov.Core.Tests/Sync/GitSyncIntegrationTests.cs`

**Context:** These tests verify the full flow: YAML loading → version resolution → cache management. They do NOT call real git repos (that would require network). Instead, they test the orchestrator with mocked git provider, and the resolver with cached files on disk.

**Step 1: Write integration tests**

```csharp
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;
using ArchGov.Core.Sync;
using FluentAssertions;
using NSubstitute;

namespace ArchGov.Core.Tests.Sync;

public sealed class GitSyncIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public GitSyncIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"archgov-sync-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void FullFlow_LoadImports_SyncMocked_ResolveFromCache()
    {
        // 1. Create imports.yaml
        var archDir = Path.Combine(_tempDir, ".architecture");
        Directory.CreateDirectory(archDir);
        File.WriteAllText(Path.Combine(archDir, "imports.yaml"), """
            imports:
              - name: shared-rules
                type: git
                url: https://github.com/acme/rules.git
                version: "^1.0"
            """);

        // 2. Load imports
        var loader = new ImportsYamlLoader();
        var manifest = loader.LoadFromFile(Path.Combine(archDir, "imports.yaml"));
        manifest.Imports.Should().ContainSingle();

        // 3. Simulate cache (as if sync had run)
        var cacheDir = Path.Combine(archDir, ".cache", "git", "shared-rules");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "shared-rules.yaml"), """
            name: shared-rules
            layers:
              - name: Domain
                allowedDependencies: []
              - name: Application
                allowedDependencies:
                  - Domain
            componentRoles:
              - name: Entity
                layer: Domain
                typeKind: Class
            """);

        File.WriteAllText(Path.Combine(cacheDir, ".git-info.json"), """
            {
                "url": "https://github.com/acme/rules.git",
                "resolvedTag": "v1.2.0",
                "commitSha": "abc123",
                "syncedAt": "2026-03-10T12:00:00+00:00"
            }
            """);

        // 4. Create user rules that extend the imported pack
        File.WriteAllText(Path.Combine(archDir, "rules.yaml"), """
            name: my-project
            extends:
              - import://shared-rules
            """);

        // 5. Resolve via RulePackResolver
        var rulePackLoader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(rulePackLoader);
        var merger = new RulePackMerger();
        var resolver = new RulePackResolver(registry, rulePackLoader, merger);

        var userPack = rulePackLoader.LoadFromFile(Path.Combine(archDir, "rules.yaml"));
        var resolved = resolver.Resolve(userPack, _tempDir);

        // Should have layers from shared-rules
        resolved.Layers.Should().Contain(l => l.Name == "Domain");
        resolved.Layers.Should().Contain(l => l.Name == "Application");
        resolved.ComponentRoles.Should().Contain(r => r.Name == "Entity");
    }

    [Fact]
    public async Task SyncOrchestrator_WithMockedGit_ShouldReportResults()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                ResolvedVersion = "v2.1.0",
                WasCached = false,
                CachePath = Path.Combine(_tempDir, callInfo.ArgAt<ImportDefinition>(0).Name),
            }));

        var orchestrator = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://a.git", Version = "^2.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://b.git", Version = "~1.0" },
            ],
        };

        var results = await orchestrator.SyncAllAsync(manifest, _tempDir, noCache: false);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.ResolvedVersion.Should().Be("v2.1.0");
        });
    }

    [Fact]
    public void CacheValidation_ShouldRespectVersionConstraints()
    {
        var cacheDir = Path.Combine(_tempDir, "my-pack");
        Directory.CreateDirectory(cacheDir);

        // Cache has v1.2.0
        var gitInfo = new GitInfo
        {
            Url = "https://github.com/test.git",
            ResolvedTag = "v1.2.0",
            CommitSha = "abc123",
            SyncedAt = DateTimeOffset.UtcNow,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(gitInfo);
        File.WriteAllText(Path.Combine(cacheDir, ".git-info.json"), json);

        // ^1.0 should be satisfied by v1.2.0
        GitCliSyncProvider.IsCacheValid(cacheDir, "https://github.com/test.git", SemVerRange.Parse("^1.0"))
            .Should().BeTrue();

        // ^2.0 should NOT be satisfied by v1.2.0
        GitCliSyncProvider.IsCacheValid(cacheDir, "https://github.com/test.git", SemVerRange.Parse("^2.0"))
            .Should().BeFalse();

        // ~1.2 should be satisfied by v1.2.0
        GitCliSyncProvider.IsCacheValid(cacheDir, "https://github.com/test.git", SemVerRange.Parse("~1.2"))
            .Should().BeTrue();

        // ~1.3 should NOT be satisfied by v1.2.0
        GitCliSyncProvider.IsCacheValid(cacheDir, "https://github.com/test.git", SemVerRange.Parse("~1.3"))
            .Should().BeFalse();
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/ArchGov.Core.Tests --filter "FullyQualifiedName~GitSyncIntegrationTests" --no-restore`
Expected: PASS (3 tests)

**Step 3: Run all tests**

Run: `dotnet test --no-restore`
Expected: All tests PASS, 0 warnings.

**Step 4: Commit**

```bash
git add tests/ArchGov.Core.Tests/Sync/GitSyncIntegrationTests.cs
git commit -m "test: add integration tests for git sync flow"
```
