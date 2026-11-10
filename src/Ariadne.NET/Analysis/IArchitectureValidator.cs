using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>Checks compiled projects against the architecture rules of a rule pack.</summary>
/// <remarks>
/// The pack must have been loaded and resolved: read by an <see cref="IRulePackLoader"/> and passed through
/// <see cref="IRulePackResolver"/>, which validates every pattern and target and applies the layer retargets and rule
/// amendments of the packs it merges. The validator trusts that. A pack built in code and passed straight in throws an
/// <see cref="ArgumentException"/> for an invalid glob or for a target that mixes layers with namespaces, assemblies,
/// types, base types, interfaces, attributes or members, and its <see cref="RulePack.LayerRetargets"/> and <see cref="RulePack.RuleAmendments"/> are ignored.
/// </remarks>
public interface IArchitectureValidator
{
    /// <summary>
    /// Checks each project and target framework, then merges the violations of the target frameworks of one project.
    /// A project that is analysed once per target framework is listed once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target frameworks of one project name must be all null or all named; mixing them throws an <see cref="ArgumentException"/>,
    /// because a violation found only in the null entry could not be told apart from one found in every target framework.
    /// </para>
    /// <para>
    /// A <c>bannedPackages</c> rule that covers a project whose <see cref="AnalyzedProject.Packages"/> is null also throws an
    /// <see cref="ArgumentException"/>, because a clean report would claim packages were checked that were not. A caller that
    /// loads projects from disk should report a missing or stale assets file before calling, as the solution validator does.
    /// </para>
    /// </remarks>
    /// <param name="resolvedPack">The resolved rule pack.</param>
    /// <param name="projects">The projects to check, one entry per project and target framework.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentException">
    /// The target frameworks of one project are mixed, or a <c>bannedPackages</c> rule covers a project whose packages are not known.
    /// </exception>
    ArchitectureReport Validate(RulePack resolvedPack, IReadOnlyList<AnalyzedProject> projects, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the first project that a <c>bannedPackages</c> rule covers while its <see cref="AnalyzedProject.Packages"/> is null.
    /// <see cref="Validate(RulePack, IReadOnlyList{AnalyzedProject}, CancellationToken)"/> throws on such a project; a caller that
    /// loads projects from disk calls this first, to report a missing or stale assets file in its own terms.
    /// </summary>
    /// <param name="resolvedPack">The resolved rule pack.</param>
    /// <param name="projects">The projects of the run.</param>
    /// <returns>The first rule and project, in rule then project order, or null when every covered project has its packages.</returns>
    PackageCoverageGap? FindPackageCoverageGap(RulePack resolvedPack, IReadOnlyList<AnalyzedProject> projects);

    /// <summary>Checks one compilation as a project named after its assembly, without a target framework.</summary>
    /// <remarks>
    /// A compilation carries no package list, so a pack with a <c>bannedPackages</c> rule that covers the compilation's assembly
    /// always throws an <see cref="ArgumentException"/>; use the overload that takes <see cref="AnalyzedProject"/> entries with
    /// their packages for such a pack.
    /// </remarks>
    /// <param name="resolvedPack">The resolved rule pack.</param>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentException">A <c>bannedPackages</c> rule covers the compilation's assembly.</exception>
    ArchitectureReport Validate(RulePack resolvedPack, Compilation compilation, CancellationToken cancellationToken = default);
}
