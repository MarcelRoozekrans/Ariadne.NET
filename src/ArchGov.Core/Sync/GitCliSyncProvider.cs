using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using ZInject;

namespace ArchGov.Core.Sync;

[Singleton]
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

        if (!noCache && IsCacheValid(importCacheDir, import.Url, versionRange))
        {
            var cachedInfo = ReadGitInfo(importCacheDir)!;
            return new SyncResult
            {
                ImportName = import.Name,
                ResolvedVersion = cachedInfo.ResolvedTag,
                WasCached = true,
                CachePath = importCacheDir,
            };
        }

        var lsRemoteOutput = await RunGitAsync($"ls-remote --tags {import.Url}", ct).ConfigureAwait(false);
        var tags = ParseTagsFromLsRemote(lsRemoteOutput);
        var bestTag = versionRange.FindBestMatch(tags);

        if (bestTag is null)
        {
            return new SyncResult
            {
                ImportName = import.Name,
                Success = false,
                Error = $"No tag satisfying '{import.Version}' found in {import.Url}",
            };
        }

        var commitSha = ParseCommitFromLsRemote(lsRemoteOutput, bestTag);

        if (Directory.Exists(importCacheDir))
        {
            Directory.Delete(importCacheDir, recursive: true);
        }

        Directory.CreateDirectory(importCacheDir);

        var cloneTarget = Path.Combine(importCacheDir, ".clone-temp");

        try
        {
            await RunGitAsync(
                $"clone --depth 1 --branch {bestTag} --single-branch {import.Url} \"{cloneTarget}\"",
                ct).ConfigureAwait(false);

            var sourceDir = string.IsNullOrEmpty(import.Path)
                ? cloneTarget
                : Path.Combine(cloneTarget, import.Path);

            CopyPackFiles(sourceDir, importCacheDir);
        }
        finally
        {
            if (Directory.Exists(cloneTarget))
            {
                Directory.Delete(cloneTarget, recursive: true);
            }
        }

        var gitInfo = new GitInfo
        {
            Url = import.Url,
            ResolvedTag = bestTag,
            CommitSha = commitSha ?? string.Empty,
            SyncedAt = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(gitInfo, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(importCacheDir, ".git-info.json"), json, ct).ConfigureAwait(false);

        return new SyncResult
        {
            ImportName = import.Name,
            ResolvedVersion = bestTag,
            WasCached = false,
            CachePath = importCacheDir,
        };
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

        if (!string.Equals(gitInfo.Url, expectedUrl, StringComparison.Ordinal))
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
    }

    private static void CopyPackFiles(string sourceDir, string targetDir)
    {
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

    private static async Task<string> RunGitAsync(string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "git {0} failed with exit code {1}: {2}",
                    arguments,
                    process.ExitCode,
                    stderr));
        }

        return stdout;
    }
}
