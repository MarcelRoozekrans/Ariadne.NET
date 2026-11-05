using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Checks compiled projects against the architecture rules of a rule pack.</summary>
/// <remarks>
/// The pack must have been loaded and resolved: read by an <see cref="IRulePackLoader"/> and passed through
/// <see cref="IRulePackResolver"/>, which validates every pattern and target and applies the layer retargets and rule
/// amendments of the packs it merges. The validator trusts that. A pack built in code and passed straight in throws an
/// <see cref="ArgumentException"/> for an invalid glob or for a target that mixes layers with namespaces, assemblies or
/// types, and its <see cref="RulePack.LayerRetargets"/> and <see cref="RulePack.RuleAmendments"/> are ignored.
/// </remarks>
public interface IArchitectureValidator
{
    /// <summary>
    /// Checks each project and target framework, then merges the violations of the target frameworks of one project.
    /// A project that is analysed once per target framework is listed once.
    /// </summary>
    /// <remarks>
    /// The target frameworks of one project name must be all null or all named; mixing them throws an <see cref="ArgumentException"/>,
    /// because a violation found only in the null entry could not be told apart from one found in every target framework.
    /// </remarks>
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
