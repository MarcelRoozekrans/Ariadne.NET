using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Checks one solution against every rule pack file in the nearest <c>.architecture</c> folder.</summary>
public interface IPackSetValidator
{
    /// <summary>
    /// Finds the pack files, loads and resolves each, loads the solution once and merges the violations of every pack in file name order.
    /// </summary>
    /// <param name="path">A solution, solution filter or project file, or a folder holding one.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The merged report, or an error when no pack is found, a pack is invalid, the solution cannot be loaded or a package list is missing.</returns>
    Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(string path, CancellationToken cancellationToken = default);
}
