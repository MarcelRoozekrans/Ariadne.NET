using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Resolves the path a user gives to the one solution, solution filter or project file to load.</summary>
internal static class SolutionPath
{
    private static readonly string[] Extensions = [".sln", ".slnx", ".slnf", ".csproj"];

    /// <summary>Returns the full path of a <c>.sln</c>, <c>.slnx</c>, <c>.slnf</c> or <c>.csproj</c> file, or of the one such file in a directory.</summary>
    /// <param name="path">A file or directory path, absolute or relative to the current directory.</param>
    public static Result<string, AriadneError> Resolve(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var full = Path.GetFullPath(path);

        if (File.Exists(full))
        {
            return IsSupported(full)
                ? Result<string, AriadneError>.Success(full)
                : Failure(AriadneErrorCode.Invalid, $"'{full}' is not a solution, solution filter or C# project file.", "Expected a .sln, .slnx, .slnf or .csproj file.");
        }

        if (!Directory.Exists(full))
        {
            return Failure(AriadneErrorCode.NotFound, $"Path '{full}' does not exist.");
        }

        var candidates = Directory.EnumerateFiles(full)
            .Where(IsSupported)
            .Order(StringComparer.Ordinal)
            .ToList();

        return candidates.Count switch
        {
            1 => Result<string, AriadneError>.Success(candidates[0]),
            0 => Failure(AriadneErrorCode.Invalid, $"Directory '{full}' holds no solution or project file.", "Expected a .sln, .slnx, .slnf or .csproj file."),
            _ => Failure(
                AriadneErrorCode.Invalid,
                $"Directory '{full}' holds more than one solution or project file.",
                "Pass one of them: " + string.Join(", ", candidates.Select(Path.GetFileName))),
        };
    }

    private static bool IsSupported(string file)
    {
        var extension = Path.GetExtension(file);
        return Array.Exists(Extensions, e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static Result<string, AriadneError> Failure(AriadneErrorCode code, string message, string? detail = null) =>
        Result<string, AriadneError>.Failure(new AriadneError(code, message, detail));
}
