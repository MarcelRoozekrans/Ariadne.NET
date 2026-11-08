using System.Collections.Frozen;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>One project handed to the analyser: its name, target framework and compiled symbols.</summary>
/// <param name="Name">The project name.</param>
/// <param name="TargetFramework">The target framework moniker, or null when it has none.</param>
/// <param name="Compilation">The project's compilation.</param>
public sealed record AnalyzedProject(string Name, string? TargetFramework, Compilation Compilation)
{
    /// <summary>Syntax trees produced by source generators; types declared only there are not checked.</summary>
    public IReadOnlySet<SyntaxTree> GeneratedTrees { get; init; } = FrozenSet<SyntaxTree>.Empty;

    /// <summary>The packages the project resolves, direct and transitive; null when they are not known, which a bannedPackages rule that covers the project does not accept.</summary>
    public IReadOnlyList<ResolvedPackage>? Packages { get; init; }
}
