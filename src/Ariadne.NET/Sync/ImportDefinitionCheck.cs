using System.Text.RegularExpressions;

using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>
/// Checks the fields sync relies on before any of them reaches git or the file system. YAML does not enforce
/// <c>required</c>, so a missing name, url or version arrives as null.
/// </summary>
internal static partial class ImportDefinitionCheck
{
    // Invalid in a file name on any platform, so an imports.yaml behaves the same on Linux and Windows:
    // the current platform's set, plus the separators, the drive colon and the Windows wildcards.
    private static readonly char[] InvalidNameChars =
        [.. Path.GetInvalidFileNameChars().Union(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])];

    // The platform's invalid path characters, plus NUL, which Path.GetFullPath rejects on every platform.
    private static readonly char[] InvalidPathChars = [.. Path.GetInvalidPathChars().Union(['\0'])];

    public static UnitResult<AriadneError> Check(ImportDefinition import, string cacheDir)
    {
        if (string.IsNullOrWhiteSpace(import.Name))
        {
            return Invalid("An import has no name; give every import in imports.yaml a name.");
        }

        if (!IsSingleDirectoryName(import.Name, cacheDir))
        {
            return Invalid(BadNameMessage(import.Name));
        }

        if (string.IsNullOrWhiteSpace(import.Url))
        {
            return Invalid($"Import '{import.Name}' has no url.");
        }

        // git is also given '--' before the url; this keeps a url that reads as an option out of every message.
        if (import.Url.StartsWith('-'))
        {
            return Invalid($"Import '{import.Name}' has url '{UrlRedaction.Redact(import.Url)}', which must not start with '-'.");
        }

        if (string.IsNullOrWhiteSpace(import.Version))
        {
            return Invalid($"Import '{import.Name}' has no version.");
        }

        return UnitResult<AriadneError>.Success();
    }

    /// <summary>
    /// Checks <paramref name="path"/>, the folder inside the repository that holds the packs: it must stay inside
    /// the clone at <paramref name="cloneDir"/>.
    /// </summary>
    public static UnitResult<AriadneError> CheckPath(ImportDefinition import, string cloneDir)
    {
        if (string.IsNullOrEmpty(import.Path))
        {
            return UnitResult<AriadneError>.Success();
        }

        // Path.GetFullPath throws on these, so they are refused before it runs.
        if (import.Path.IndexOfAny(InvalidPathChars) >= 0)
        {
            return Invalid($"Import '{import.Name}' has path '{import.Path}', which contains a character that is not allowed in a path.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cloneDir));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, import.Path)));
        var inside = !Path.IsPathRooted(import.Path)
            && (string.Equals(full, root, StringComparison.Ordinal)
                || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

        if (!inside)
        {
            return Invalid($"Import '{import.Name}' has path '{import.Path}', which must be a relative folder inside the repository.");
        }

        // git's own data, including .git/config with any credentials, is never a pack folder. On NTFS the 8.3 short
        // name GIT~1 also reaches .git, so it is refused as git's core.protectNTFS does.
        var firstSegment = Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar)[0];
        return string.Equals(firstSegment, ".git", StringComparison.OrdinalIgnoreCase) || GitShortName().IsMatch(firstSegment)
            ? Invalid($"Import '{import.Name}' has path '{import.Path}', which points into the repository's .git folder.")
            : UnitResult<AriadneError>.Success();
    }

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

    private static UnitResult<AriadneError> Invalid(string message) =>
        UnitResult<AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, message));

    [GeneratedRegex(@"^git~[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GitShortName();
}
