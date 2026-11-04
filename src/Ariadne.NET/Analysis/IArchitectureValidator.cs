using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Checks compiled projects against the architecture rules of a rule pack.</summary>
public interface IArchitectureValidator
{
    /// <summary>
    /// Checks each project and target framework, then merges the violations of the target frameworks of one project.
    /// A project that is analysed once per target framework is listed once.
    /// </summary>
    /// <param name="resolvedPack">The resolved rule pack.</param>
    /// <param name="projects">The projects to check, one entry per project and target framework.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    ArchitectureReport Validate(RulePack resolvedPack, IReadOnlyList<AnalyzedProject> projects, CancellationToken cancellationToken = default);

    /// <summary>Checks one compilation as a project named after its assembly, without a target framework.</summary>
    /// <param name="resolvedPack">The resolved rule pack.</param>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    ArchitectureReport Validate(RulePack resolvedPack, Compilation compilation, CancellationToken cancellationToken = default);
}
