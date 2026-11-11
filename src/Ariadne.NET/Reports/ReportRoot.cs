namespace Ariadne.NET.Reports;

/// <summary>Finds the folder report paths are made relative to.</summary>
public static class ReportRoot
{
    /// <summary>
    /// Finds the repository root: the nearest folder, starting at <paramref name="solutionFolder"/> and walking up,
    /// that holds a <c>.git</c> file or folder.
    /// </summary>
    /// <param name="solutionFolder">The folder of the solution or project being checked.</param>
    /// <returns>The full path of that folder, or of <paramref name="solutionFolder"/> when no ancestor has a <c>.git</c> entry.</returns>
    public static string Find(string solutionFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(solutionFolder);

        var start = Path.GetFullPath(solutionFolder);
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                return dir.FullName;
            }
        }

        return start;
    }
}
