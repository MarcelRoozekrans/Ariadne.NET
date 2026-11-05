using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Checks a solution or project on disk against the architecture rules of a rule pack.</summary>
/// <remarks>
/// The pack must have been loaded and resolved by <see cref="IRulePackResolver"/>, as for
/// <see cref="IArchitectureValidator"/>: an unresolved pack built in code may throw an <see cref="ArgumentException"/> for
/// an invalid glob or a mixed target, and its layer retargets and rule amendments are ignored.
/// </remarks>
public interface ISolutionValidator
{
    /// <summary>Loads <paramref name="path"/>, checks its projects, lists the skipped ones in the report and releases the workspace.</summary>
    /// <param name="resolvedPack">The resolved rule pack.</param>
    /// <param name="path">A solution, solution filter or project file, or a directory that holds exactly one.</param>
    /// <param name="cancellationToken">Cancels the load and the check.</param>
    Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(RulePack resolvedPack, string path, CancellationToken cancellationToken = default);
}
