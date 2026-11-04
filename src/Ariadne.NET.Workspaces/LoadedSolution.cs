using Ariadne.NET.Analysis;

namespace Ariadne.NET.Workspaces;

/// <summary>The C# projects of a loaded solution or project, ready to check. Disposing it releases the workspace that holds them.</summary>
public sealed class LoadedSolution : IDisposable
{
    private readonly IDisposable? _owner;

    internal LoadedSolution(
        string rootDirectory,
        IReadOnlyList<AnalyzedProject> projects,
        IReadOnlyList<SkippedProject> skippedProjects,
        IDisposable? owner)
    {
        RootDirectory = rootDirectory;
        Projects = projects;
        SkippedProjects = skippedProjects;
        _owner = owner;
    }

    /// <summary>The directory of the solution, solution filter or project file that was loaded.</summary>
    public string RootDirectory { get; }

    /// <summary>The projects to check, one entry per project and target framework.</summary>
    public IReadOnlyList<AnalyzedProject> Projects { get; }

    /// <summary>The named projects that are not checked, with the reason.</summary>
    public IReadOnlyList<SkippedProject> SkippedProjects { get; }

    /// <summary>Disposes the workspace the compilations came from.</summary>
    public void Dispose() => _owner?.Dispose();
}
