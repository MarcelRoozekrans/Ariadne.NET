using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Testing;

/// <summary>Finds the solution a check runs on when the caller names none.</summary>
internal static class SolutionLocator
{
    /// <summary>
    /// Walks up from <paramref name="startFolder"/> to the nearest folder that holds at least one <c>.sln</c> or <c>.slnx</c> file.
    /// </summary>
    /// <param name="startFolder">The folder the walk starts in; it is searched itself first.</param>
    /// <returns>
    /// The full path of the one solution in that folder; <c>NotFound</c> when no folder up to the root holds one, and
    /// <c>Invalid</c>, with the file names as detail, when the nearest such folder holds more than one.
    /// </returns>
    public static Result<string, AriadneError> Find(string startFolder)
    {
        ArgumentNullException.ThrowIfNull(startFolder);

        var start = Path.GetFullPath(startFolder);
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            List<string> solutions;
            try
            {
                solutions = SolutionsIn(dir.FullName);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return Result<string, AriadneError>.Failure(
                    new AriadneError(AriadneErrorCode.Invalid, $"Folder '{dir.FullName}' cannot be read while looking for a solution.", ex.Message));
            }

            switch (solutions.Count)
            {
                case 0:
                    continue;
                case 1:
                    return Result<string, AriadneError>.Success(solutions[0]);
                default:
                    return Result<string, AriadneError>.Failure(new AriadneError(
                        AriadneErrorCode.Invalid,
                        $"Folder '{dir.FullName}' holds more than one solution file; pass the path of the one to check.",
                        string.Join(Environment.NewLine, solutions.ConvertAll(Path.GetFileName))));
            }
        }

        return Result<string, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.NotFound,
            $"No .sln or .slnx file was found in '{start}' or any folder above it.",
            "Pass the path of the solution to check."));
    }

    // The .sln and .slnx files directly in the folder, in ordinal name order.
    private static List<string> SolutionsIn(string folder)
    {
        var solutions = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            if (IsSolution(file))
            {
                solutions.Add(file);
            }
        }

        solutions.Sort(StringComparer.Ordinal);
        return solutions;
    }

    private static bool IsSolution(string file)
    {
        var extension = Path.GetExtension(file);
        return string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase);
    }
}
