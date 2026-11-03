namespace Ariadne.NET.Sync;

/// <summary>
/// Finds symbolic links in the part of a clone that sync copies. Copying follows a link to its target, so a
/// repository could otherwise pull any local file or folder into the cache.
/// </summary>
internal static class CloneLinkCheck
{
    /// <summary>
    /// Returns the first link, as a path relative to <paramref name="cloneDir"/>, among the folders from the clone
    /// down to <paramref name="sourceDir"/> and everything below it except its <c>.git</c> folder; or null.
    /// </summary>
    public static string? FindLink(string cloneDir, string sourceDir)
    {
        // The import's path may itself pass through a link, such as 'packs' pointing at /etc.
        var current = cloneDir;
        foreach (var segment in Path.GetRelativePath(cloneDir, sourceDir)
                     .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            current = Path.Combine(current, segment);
            if (IsLink(new DirectoryInfo(current)))
            {
                return Path.GetRelativePath(cloneDir, current);
            }
        }

        var root = new DirectoryInfo(sourceDir);
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if (IsLink(entry))
                {
                    return Path.GetRelativePath(cloneDir, entry.FullName);
                }

                // The same .git folder the copy skips: git's own data, never copied.
                var isGitFolder = ReferenceEquals(directory, root)
                    && string.Equals(entry.Name, ".git", StringComparison.Ordinal);
                if (entry is DirectoryInfo subdirectory && !isGitFolder)
                {
                    pending.Push(subdirectory);
                }
            }
        }

        return null;
    }

    // LinkTarget catches symlinks, including dangling ones; ReparsePoint also catches Windows junctions.
    private static bool IsLink(FileSystemInfo entry) =>
        entry.LinkTarget is not null
        || (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint));
}
