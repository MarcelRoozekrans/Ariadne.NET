namespace Ariadne.NET;

/// <summary>
/// The rule for an import name, shared by sync, which writes the import's cache directory, and the resolver, which
/// reads <c>import://</c> packs from it.
/// </summary>
internal static class ImportNameRule
{
    // Invalid in a file name on any platform, so an imports.yaml behaves the same on Linux and Windows:
    // the current platform's set, plus the separators, the drive colon and the Windows wildcards.
    private static readonly char[] InvalidNameChars =
        [.. Path.GetInvalidFileNameChars().Union(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])];

    /// <summary>The message for an import name that <see cref="IsSingleDirectoryName"/> refuses.</summary>
    internal static string BadNameMessage(string name) =>
        $"Import name '{name}' must be a plain directory name: no path separators, drive, '.' or '..', or characters such as ':' or '*'.";

    /// <summary>
    /// Whether <paramref name="name"/> names one directory directly under <paramref name="cacheDir"/>. Sync deletes and
    /// recreates that directory, and the resolver reads <c>import://</c> packs from it, so neither may leave the cache.
    /// </summary>
    internal static bool IsSingleDirectoryName(string name, string cacheDir)
    {
        if (name is "." or ".." || Path.IsPathRooted(name) || name.IndexOfAny(InvalidNameChars) >= 0)
        {
            return false;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cacheDir));
        var parent = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, name)));
        return string.Equals(parent, root, StringComparison.Ordinal);
    }
}
