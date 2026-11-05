using System.Text.Json;
using System.Xml;
using Ariadne.NET.Abstractions;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Finds the project files a resolved path names, which are the ones that get checked.</summary>
internal static class NamedProjects
{
    /// <summary>Compares normalised full paths: case-insensitive on Windows, exact elsewhere.</summary>
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// The full paths of the projects <paramref name="file"/> names: every project a <c>.sln</c> or <c>.slnx</c> lists, the listed
    /// projects of a <c>.slnf</c>, or the file itself for a <c>.csproj</c>. Projects these reference are not named.
    /// </summary>
    /// <remarks>
    /// MSBuildWorkspace also returns the projects a listed project references, whichever kind of file it opens, so the named
    /// projects are read from the file itself.
    /// </remarks>
    /// <param name="file">A full path that <see cref="SolutionPath.Resolve"/> returned.</param>
    /// <param name="cancellationToken">Cancels reading a solution file.</param>
    public static async Task<Result<IReadOnlySet<string>, AriadneError>> ReadAsync(string file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var extension = Path.GetExtension(file);
        if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return Result<IReadOnlySet<string>, AriadneError>.Success(new HashSet<string>([Normalize(file)], PathComparer));
        }

        return string.Equals(extension, ".slnf", StringComparison.OrdinalIgnoreCase)
            ? ReadFilter(file)
            : await ReadSolutionAsync(file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The full form of <paramref name="path"/>, with either separator accepted and no trailing separator.</summary>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Replace('\\', '/')));
    }

    private static async Task<Result<IReadOnlySet<string>, AriadneError>> ReadSolutionAsync(string file, CancellationToken cancellationToken)
    {
        var serializer = SolutionSerializers.GetSerializerByMoniker(file);
        if (serializer is null)
        {
            return Invalid("Solution", file, "The file is not a .sln or .slnx solution.");
        }

        try
        {
            var model = await serializer.OpenAsync(file, cancellationToken).ConfigureAwait(false);
            var directory = Path.GetDirectoryName(file)!;
            var named = new HashSet<string>(PathComparer);
            foreach (var project in model.SolutionProjects)
            {
                named.Add(Normalize(Path.Combine(directory, project.FilePath)));
            }

            return Result<IReadOnlySet<string>, AriadneError>.Success(named);
        }
        catch (Exception ex) when (ex is FormatException or XmlException or IOException or UnauthorizedAccessException)
        {
            return Invalid("Solution", file, ex.Message);
        }
    }

    // A solution filter is {"solution": {"path": "<solution>", "projects": ["<project>", ...]}}; the solution path is relative to the
    // filter and the project paths to the solution.
    private static Result<IReadOnlySet<string>, AriadneError> ReadFilter(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("solution", out var solution)
                || solution.ValueKind != JsonValueKind.Object
                || !solution.TryGetProperty("path", out var solutionPath)
                || solutionPath.ValueKind != JsonValueKind.String
                || !solution.TryGetProperty("projects", out var projects)
                || projects.ValueKind != JsonValueKind.Array)
            {
                return Invalid("Solution filter", file, "Expected an object 'solution' with a string 'path' and an array 'projects'.");
            }

            var solutionFile = Path.Combine(Path.GetDirectoryName(file)!, solutionPath.GetString()!.Replace('\\', '/'));
            var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionFile))!;
            var named = new HashSet<string>(PathComparer);
            foreach (var project in projects.EnumerateArray())
            {
                if (project.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(project.GetString()))
                {
                    return Invalid("Solution filter", file, "Every entry of 'projects' must be a project path.");
                }

                named.Add(Normalize(Path.Combine(solutionDirectory, project.GetString()!)));
            }

            return Result<IReadOnlySet<string>, AriadneError>.Success(named);
        }
        catch (JsonException ex)
        {
            return Invalid("Solution filter", file, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Invalid("Solution filter", file, ex.Message);
        }
    }

    private static Result<IReadOnlySet<string>, AriadneError> Invalid(string kind, string file, string reason) =>
        Result<IReadOnlySet<string>, AriadneError>.Failure(
            new AriadneError(AriadneErrorCode.Invalid, $"{kind} '{file}' cannot be read.", $"{file}: {reason}"));
}
