using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Finds every pack file in the nearest <c>.architecture</c> folder above a solution.</summary>
internal static class PackDiscovery
{
    private const string FolderName = ".architecture";

    /// <summary>Walks up from the solution's folder to the first folder that holds <c>.architecture</c> and lists its pack files.</summary>
    /// <param name="solutionFile">A solution, solution filter or project file, or a folder holding one.</param>
    public static Result<DiscoveredPacks, AriadneError> Find(string solutionFile)
    {
        var resolved = SolutionPath.Resolve(solutionFile);
        if (resolved.IsFailure)
        {
            return Result<DiscoveredPacks, AriadneError>.Failure(resolved.Error);
        }

        var start = Path.GetDirectoryName(resolved.Value)!;
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var architecture = Path.Combine(dir.FullName, FolderName);
            if (!Directory.Exists(architecture))
            {
                continue;
            }

            return List(dir.FullName, architecture);
        }

        return Failure($"No '{FolderName}' folder was found in '{start}' or any folder above it.");
    }

    private static Result<DiscoveredPacks, AriadneError> List(string projectRoot, string architecture)
    {
        string[] files;
        try
        {
            files = Directory.EnumerateFiles(architecture)
                .Where(IsPackFile)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return Result<DiscoveredPacks, AriadneError>.Failure(
                new AriadneError(AriadneErrorCode.Invalid, $"Folder '{architecture}' cannot be read.", ex.Message));
        }

        return files.Length == 0
            ? Failure($"Folder '{architecture}' holds no .yaml or .yml pack file.")
            : Result<DiscoveredPacks, AriadneError>.Success(new DiscoveredPacks(projectRoot, files));
    }

    private static bool IsPackFile(string file)
    {
        // imports.yaml is the 'ariadne sync' manifest, not a rule pack.
        var name = Path.GetFileNameWithoutExtension(file);
        if (string.Equals(name, "imports", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(file);
        return string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static Result<DiscoveredPacks, AriadneError> Failure(string message) =>
        Result<DiscoveredPacks, AriadneError>.Failure(new AriadneError(AriadneErrorCode.NotFound, message));
}
