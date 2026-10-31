using System.Text.Json;

using Ariadne.NET.Abstractions;
using Ariadne.NET.Telemetry;

using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>
/// The one implementation of syncing an import. <see cref="Instrumented"/> wraps it in the generated proxy, which
/// <see cref="GitCliSyncProvider"/> calls with its git client: the resilience proxy around <see cref="GitCliClient"/>.
/// </summary>
internal sealed class SyncOperations : ISyncOperations
{
    private SyncOperations()
    {
    }

    /// <summary>Gets the instrumented operation <see cref="GitCliSyncProvider"/> calls.</summary>
    public static ISyncOperations Instrumented { get; } = new SyncOperationsInstrumented(new SyncOperations());

    public async Task<Result<SyncedImport, AriadneError>> SyncImportAsync(
        ImportDefinition import,
        string cacheDir,
        bool noCache,
        IGitClient git,
        CancellationToken ct)
    {
        var checkedImport = ImportDefinitionCheck.Check(import, cacheDir);
        if (checkedImport.IsFailure)
        {
            return Failure(checkedImport.Error);
        }

        var parsedRange = SemVerRange.Parse(import.Version);
        if (parsedRange.IsFailure)
        {
            return Failure(parsedRange.Error);
        }

        var importCacheDir = Path.Combine(cacheDir, import.Name);
        if (!noCache && IsCacheValid(importCacheDir, import.Url, parsedRange.Value))
        {
            var cachedInfo = ReadGitInfo(importCacheDir)!;
            await RedactLegacyGitInfoAsync(cachedInfo, importCacheDir, ct).ConfigureAwait(false);
            return Success(new SyncedImport(import.Name, cachedInfo.ResolvedTag, WasCached: true, importCacheDir));
        }

        // "--" ends git's options, so the url is read as the repository even if it looks like an option.
        var lsRemote = await git.RunAsync(["ls-remote", "--tags", "--", import.Url], ct).ConfigureAwait(false);
        if (lsRemote.IsFailure)
        {
            return Failure(GitUnavailable(import, lsRemote.Error));
        }

        var bestTag = parsedRange.Value.FindBestMatch(ParseTagsFromLsRemote(lsRemote.Value));
        if (bestTag is null)
        {
            return Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"No tag satisfying '{import.Version}' found in {UrlRedaction.Redact(import.Url)}"));
        }

        var commitSha = ParseCommitFromLsRemote(lsRemote.Value, bestTag);
        var fetched = await FetchIntoCacheAsync(import, bestTag, commitSha, cacheDir, git, ct).ConfigureAwait(false);
        return fetched.IsFailure
            ? Failure(fetched.Error)
            : Success(new SyncedImport(import.Name, bestTag, WasCached: false, importCacheDir));
    }

    /// <summary>
    /// Clones <paramref name="tag"/> and builds the cache entry in a staging directory beside it, then swaps it in.
    /// A failed fetch leaves the previous cache entry as it was. An IO failure is Unavailable for this import only.
    /// </summary>
    private static async Task<UnitResult<AriadneError>> FetchIntoCacheAsync(
        ImportDefinition import, string tag, string? commitSha, string cacheDir, IGitClient git, CancellationToken ct)
    {
        var importCacheDir = Path.Combine(cacheDir, import.Name);
        var stagingDir = Path.Combine(cacheDir, $".{import.Name}.staging-{Guid.NewGuid():N}");
        var cloneDir = Path.Combine(stagingDir, "clone");
        var packDir = Path.Combine(stagingDir, "pack");

        var checkedPath = ImportDefinitionCheck.CheckPath(import, cloneDir);
        if (checkedPath.IsFailure)
        {
            return checkedPath;
        }

        try
        {
            Directory.CreateDirectory(stagingDir);
            var cloned = await git.RunAsync(
                ["clone", "--depth", "1", "--branch", tag, "--single-branch", "--", import.Url, cloneDir],
                ct).ConfigureAwait(false);
            if (cloned.IsFailure)
            {
                return UnitResult<AriadneError>.Failure(GitUnavailable(import, cloned.Error));
            }

            var sourceDir = string.IsNullOrEmpty(import.Path) ? cloneDir : Path.Combine(cloneDir, import.Path);
            var checkedSource = CheckSource(import, tag, cloneDir, sourceDir);
            if (checkedSource.IsFailure)
            {
                return checkedSource;
            }

            CopyPackFiles(sourceDir, packDir);
            await WriteGitInfoAsync(import.Url, tag, commitSha, packDir, ct).ConfigureAwait(false);

            if (Directory.Exists(importCacheDir))
            {
                Directory.Delete(importCacheDir, recursive: true);
            }

            Directory.Move(packDir, importCacheDir);
            return UnitResult<AriadneError>.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return UnitResult<AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Unavailable,
                $"Import '{import.Name}' could not be written to the cache at '{importCacheDir}'.",
                ex.Message));
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    /// <summary>
    /// Checks the cloned folder that will be copied: it must exist, and neither it nor the way to it may hold a link.
    /// </summary>
    private static UnitResult<AriadneError> CheckSource(ImportDefinition import, string tag, string cloneDir, string sourceDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return UnitResult<AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"Import '{import.Name}' has path '{import.Path}', which does not exist in the repository at {tag}."));
        }

        var link = CloneLinkCheck.FindLink(cloneDir, sourceDir);
        return link is null
            ? UnitResult<AriadneError>.Success()
            : UnitResult<AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"Import '{import.Name}' contains a symbolic link at '{link}'; pack files must be regular files inside the repository."));
    }

    /// <summary>Removes the staging directory; one that cannot be removed is left behind rather than hiding the outcome.</summary>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover staging directory is harmless: its name is unique and nothing reads it.
        }
    }

    /// <summary>
    /// The proxy reports a timeout as the cancellation it caused, whose message says nothing about git;
    /// a caller's own cancellation never gets here, because the proxy rethrows it.
    /// </summary>
    private static AriadneError GitUnavailable(ImportDefinition import, ResilienceError error) =>
        new(
            AriadneErrorCode.Unavailable,
            $"git failed for import '{import.Name}' at {UrlRedaction.Redact(import.Url)}.",
            error.InnerException is OperationCanceledException ? "git did not finish before the timeout." : error.Reason);

    private static Result<SyncedImport, AriadneError> Success(SyncedImport synced) =>
        Result<SyncedImport, AriadneError>.Success(synced);

    private static Result<SyncedImport, AriadneError> Failure(AriadneError error) =>
        Result<SyncedImport, AriadneError>.Failure(error);

    private static Task WriteGitInfoAsync(string url, string tag, string? commitSha, string importCacheDir, CancellationToken ct) =>
        WriteGitInfoAsync(
            new GitInfo
            {
                Url = url,
                ResolvedTag = tag,
                CommitSha = commitSha ?? string.Empty,
                SyncedAt = DateTimeOffset.UtcNow,
            },
            importCacheDir,
            ct);

    private static async Task WriteGitInfoAsync(GitInfo gitInfo, string importCacheDir, CancellationToken ct)
    {
        // Never store credentials; the cache check compares redacted urls.
        var redacted = new GitInfo
        {
            Url = UrlRedaction.Redact(gitInfo.Url),
            ResolvedTag = gitInfo.ResolvedTag,
            CommitSha = gitInfo.CommitSha,
            SyncedAt = gitInfo.SyncedAt,
        };

        var json = JsonSerializer.Serialize(redacted, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(importCacheDir, ".git-info.json"), json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Rewrites a record written before urls were redacted, so its credentials do not stay on disk until the next
    /// fetch. The cache is still valid, so a failed rewrite leaves the record as it was and the sync succeeds.
    /// </summary>
    private static async Task RedactLegacyGitInfoAsync(GitInfo cachedInfo, string importCacheDir, CancellationToken ct)
    {
        if (string.Equals(cachedInfo.Url, UrlRedaction.Redact(cachedInfo.Url), StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await WriteGitInfoAsync(cachedInfo, importCacheDir, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next fetch writes a redacted record; serving the valid cache matters more.
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

            if (!refName.StartsWith("refs/tags/", StringComparison.Ordinal))
            {
                continue;
            }

            var tagName = refName["refs/tags/".Length..];

            if (tagName.EndsWith("^{}", StringComparison.Ordinal))
            {
                continue;
            }

            if (!SemVer.TryParse(tagName, out _))
            {
                continue;
            }

            tags.Add(tagName);
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
            var currentSha = parts[0].Trim();

            if (string.Equals(refName, $"refs/tags/{tagName}^{{}}", StringComparison.Ordinal))
            {
                return currentSha;
            }

            if (string.Equals(refName, $"refs/tags/{tagName}", StringComparison.Ordinal))
            {
                sha = currentSha;
            }
        }

        return sha;
    }

    public static bool IsCacheValid(string cacheDir, string expectedUrl, SemVerRange versionRange)
    {
        if (!Directory.Exists(cacheDir))
        {
            return false;
        }

        var gitInfo = ReadGitInfo(cacheDir);
        if (gitInfo is null)
        {
            return false;
        }

        // Redacted on both sides: the record holds no credentials, and a cache written before redaction still matches.
        if (!string.Equals(UrlRedaction.Redact(gitInfo.Url), UrlRedaction.Redact(expectedUrl), StringComparison.Ordinal))
        {
            return false;
        }

        if (!SemVer.TryParse(gitInfo.ResolvedTag, out var cachedVersion))
        {
            return false;
        }

        return versionRange.IsSatisfiedBy(cachedVersion!);
    }

    private static GitInfo? ReadGitInfo(string cacheDir)
    {
        var filePath = Path.Combine(cacheDir, ".git-info.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<GitInfo>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable record counts as no cache; the fetch that follows reports any IO failure.
            return null;
        }
    }

    private static void CopyPackFiles(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, dirPath);

            if (relativePath.StartsWith(".git", StringComparison.Ordinal)
                && (relativePath.Length == 4
                    || relativePath[4] == Path.DirectorySeparatorChar
                    || relativePath[4] == Path.AltDirectorySeparatorChar))
            {
                continue;
            }

            Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
        }

        foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, filePath);

            if (relativePath.StartsWith(".git", StringComparison.Ordinal)
                && (relativePath.Length == 4
                    || relativePath[4] == Path.DirectorySeparatorChar
                    || relativePath[4] == Path.AltDirectorySeparatorChar))
            {
                continue;
            }

            var destPath = Path.Combine(targetDir, relativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null)
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(filePath, destPath, overwrite: true);
        }
    }
}
