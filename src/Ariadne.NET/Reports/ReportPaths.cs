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
}
