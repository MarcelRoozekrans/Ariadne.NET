using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Workspaces;

/// <summary>
/// Finds the analyzer references of a project that the compiler in this process cannot load, so a compile failure caused by a
/// source generator that never ran names that generator instead of only the errors that follow from it.
/// </summary>
/// <remarks>
/// The workspace raises <c>AnalyzerFileReference.AnalyzerLoadFailed</c> once, while it compiles, and caches the result, so the
/// failure cannot be asked for afterwards. The check therefore reads each analyzer file's metadata, as the compiler's own
/// newer-compiler check does, and loads nothing.
/// </remarks>
internal static class AnalyzerLoadCheck
{
    private const string CompilerAssemblyName = "Microsoft.CodeAnalysis";

    /// <summary>The version of Microsoft.CodeAnalysis this process runs; an analyzer built against a newer one is refused.</summary>
    public static Version RunningCompilerVersion { get; } = typeof(Compilation).Assembly.GetName().Version!;

    /// <summary>
    /// One <c>path: problem</c> line per analyzer reference of <paramref name="project"/> that cannot be loaded, with the path
    /// relative to <paramref name="rootDirectory"/> when it lies under it.
    /// </summary>
    public static List<(string Name, string Line)> Failures(Project project, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        var failures = new List<(string Name, string Line)>();
        foreach (var reference in project.AnalyzerReferences)
        {
            if (reference.FullPath is { Length: > 0 } path && Problem(path) is { } problem)
            {
                failures.Add((Path.GetFileNameWithoutExtension(path), $"{Display(rootDirectory, path)}: {problem}"));
            }
        }

        return failures;
    }

    /// <summary>Why the compiler cannot load the analyzer assembly at <paramref name="path"/>, or null when its metadata allows it.</summary>
    public static string? Problem(string path)
    {
        if (!File.Exists(path))
        {
            return "file not found";
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
            {
                return "not a .NET assembly";
            }

            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                if (reader.StringComparer.Equals(reference.Name, CompilerAssemblyName) && reference.Version > RunningCompilerVersion)
                {
                    return $"built against {CompilerAssemblyName} {reference.Version}, newer than the {RunningCompilerVersion} that "
                        + $"Ariadne.NET runs; an Ariadne.NET release on {CompilerAssemblyName} {reference.Version} or later can load it";
                }
            }

            return null;
        }
        catch (BadImageFormatException)
        {
            return "not a .NET assembly";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"cannot be read: {ex.Message}";
        }
    }

    // Relative under the root, so a fixture or repository path reads short; an SDK or NuGet cache path stays absolute.
    private static string Display(string rootDirectory, string path)
    {
        var relative = Path.GetRelativePath(rootDirectory, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }
}
