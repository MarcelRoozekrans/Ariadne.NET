namespace Ariadne.NET.Reports;

internal static class ReportPaths
{
    /// <summary>The path relative to the root, or as given when outside it, with <c>/</c> separators.</summary>
    public static string Relative(string root, string filePath)
    {
        TryRelative(root, filePath, out var path);
        return path;
    }

    /// <summary>
    /// Makes <paramref name="filePath"/> relative to <paramref name="root"/> with <c>/</c> separators.
    /// Returns <see langword="false"/> and the path as given, with <c>/</c> separators, when it lies outside the root.
    /// </summary>
    public static bool TryRelative(string root, string filePath, out string path)
    {
        var relative = Path.GetRelativePath(root, filePath);
        var outside = Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith("../", StringComparison.Ordinal)
            || relative.StartsWith("..\\", StringComparison.Ordinal);
        path = (outside ? filePath : relative).Replace('\\', '/');
        return !outside;
    }

    /// <summary>The <c>file://</c> uri of an absolute path, each segment escaped so that <c>#</c> and <c>%</c> survive.</summary>
    public static string ToFileUri(string fullPath) =>
        FileUriFromNormalized(OperatingSystem.IsWindows() ? fullPath.Replace('\\', '/') : fullPath);

    /// <summary>
    /// Builds the uri from an absolute path that already uses <c>/</c> separators: <c>/a/b</c>, <c>C:/a/b</c> or the
    /// UNC form <c>//host/share/x</c>. A trailing <c>/</c> is kept.
    /// </summary>
    internal static string FileUriFromNormalized(string path)
    {
        var authority = string.Empty;
        var rest = path;
        if (path.StartsWith("//", StringComparison.Ordinal))
        {
            var end = path.IndexOf('/', 2);
            authority = end < 0 ? path[2..] : path[2..end];
            rest = end < 0 ? string.Empty : path[end..];
        }

        if (rest.StartsWith('/'))
        {
            rest = rest[1..];
        }

        var segments = rest.Split('/');
        var hasDrive = segments.Length > 0 && segments[0].Length == 2 && char.IsAsciiLetter(segments[0][0]) && segments[0][1] == ':';
        var escaped = segments.Select((segment, i) => i == 0 && hasDrive ? segment : Uri.EscapeDataString(segment));
        return "file://" + authority + "/" + string.Join('/', escaped);
    }
}
