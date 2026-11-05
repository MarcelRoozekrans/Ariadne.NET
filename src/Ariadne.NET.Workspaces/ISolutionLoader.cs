using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Loads a solution, solution filter or C# project into compilations.</summary>
public interface ISolutionLoader
{
    /// <summary>
    /// Loads the projects <paramref name="path"/> names: every project of a <c>.sln</c> or <c>.slnx</c>, the listed projects of a
    /// <c>.slnf</c>, or the one project of a <c>.csproj</c>. A load that is not complete is an error.
    /// </summary>
    /// <param name="path">A solution, solution filter or project file, or a directory that holds exactly one.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    Task<Result<LoadedSolution, AriadneError>> LoadAsync(string path, CancellationToken cancellationToken = default);
}
