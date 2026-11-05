namespace Ariadne.NET.Analysis;

/// <summary>The outcome of checking projects against architecture rules.</summary>
public sealed record ArchitectureReport
{
    /// <summary>The violations found.</summary>
    public required IReadOnlyList<ArchitectureViolation> Violations { get; init; }

    /// <summary>The projects that were analysed.</summary>
    public required IReadOnlyList<CheckedProject> Projects { get; init; }

    /// <summary>The projects that were not analysed.</summary>
    public IReadOnlyList<SkippedProject> SkippedProjects { get; init; } = [];

    /// <summary>The number of types that were checked.</summary>
    public required int TypesChecked { get; init; }

    /// <summary>True when no violations were found.</summary>
    public bool IsValid => Violations.Count == 0;
}
